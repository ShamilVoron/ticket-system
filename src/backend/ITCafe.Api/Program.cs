using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ITCafe.Api.Data;
using ITCafe.Api.Authentication;
using ITCafe.Api.Data.Seeding;
using ITCafe.Api.HealthChecks;
using ITCafe.Api.Hubs;
using ITCafe.Api.Middleware;
using ITCafe.Api.Services;
using ITCafe.Api.Services.Contracts;
using ITCafe.Api.Services.Implementations;
using ITCafe.Api.SignalR;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MassTransit;
using Serilog;

namespace ITCafe.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((ctx, lc) => lc
                .WriteTo.Console()
                .ReadFrom.Configuration(ctx.Configuration));

            // 1. Relational DB (PostgreSQL / InMemory for tests)
            if (builder.Environment.IsEnvironment("Test"))
            {
                var testDbName = builder.Configuration["TestDbName"] ?? "TestDb";
                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(testDbName));
            }
            else
            {
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

                var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
                if (!string.IsNullOrWhiteSpace(dbPassword))
                {
                    connectionString += $";Password={dbPassword}";
                }

                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString));
            }

            // 2. NoSQL DB (MongoDB)
            builder.Services.AddSingleton<MongoDbContext>();

            // 3. Real-time (SignalR)
            builder.Services.AddSignalR().AddJsonProtocol(o =>
            {
                o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            });
            builder.Services.AddSingleton<TicketRealtimeBroadcaster>();

            // 4. Messaging (MassTransit + RabbitMQ)
            builder.Services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
                    var rabbitPort = builder.Configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672;
                    var rabbitUsername = builder.Configuration.GetValue<string>("RabbitMQ:Username")
                        ?? Environment.GetEnvironmentVariable("RABBITMQ_USERNAME")
                        ?? "guest";
                    var rabbitPassword = builder.Configuration.GetValue<string>("RabbitMQ:Password")
                        ?? Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
                        ?? "guest";
                    var rabbitUri = new Uri($"rabbitmq://{rabbitHost}:{rabbitPort}/");
                    cfg.Host(rabbitUri, h =>
                    {
                        h.Username(rabbitUsername);
                        h.Password(rabbitPassword);
                    });
                });
            });

            // 5. Services
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IEmployeeService, EmployeeService>();
            builder.Services.AddScoped<ISlaService, SlaService>();
            builder.Services.AddScoped<ITelegramNotificationService, TelegramNotificationService>();
            builder.Services.AddScoped<IOkdeskSyncService, OkdeskSyncService>();
            builder.Services.AddScoped<IMessengerService, MessengerService>();
            builder.Services.AddSingleton<ChatRealtimeBroadcaster>();
            builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
            builder.Services.AddHttpClient();

            // 6. FluentValidation
            builder.Services.AddFluentValidationAutoValidation();

            // 7. OpenAPI/Swagger
            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            // 8. Health Checks
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("postgresql")
                .AddCheck<RabbitMqHealthCheck>("rabbitmq");

            // 9. Rate Limiting
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("fixed", opt =>
                {
                    opt.PermitLimit = 120;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 10;
                });

                // Жёстче лимит на запись в мессенджер (на пользователя): спам / скомпрометированный токен.
                options.AddPolicy("messenger_write", context =>
                {
                    var uid = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User?.FindFirstValue("sub")
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"messenger:write:{uid}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 45,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        });
                });

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Try again later.", token);
                };
            });

            // 10. CORS for Frontend
            var corsExtraOrigins = (builder.Configuration.GetSection("Cors:ExtraOrigins").Get<string[]>()
                    ?? Array.Empty<string>())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    // Локальная сеть: иначе браузер режет cross-origin ответ и справочники «пустые»
                    static bool IsAllowedDevOrigin(string? origin)
                    {
                        if (string.IsNullOrWhiteSpace(origin)) return false;
                        try
                        {
                            var u = new Uri(origin);
                            if (u.Scheme is not ("http" or "https")) return false;
                            if (u.Host is "localhost" or "127.0.0.1" or "[::1]") return u.Port is 3000 or 3011 or 0;
                            if (!IPAddress.TryParse(u.Host, out var ip)) return false;
                            var b = ip.GetAddressBytes();
                            if (b.Length != 4) return false;
                            if (b[0] == 10) return true;
                            if (b[0] == 192 && b[1] == 168) return true;
                            if (b[0] == 172 && b[1] is >= 16 and <= 31) return true;
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            if (string.IsNullOrWhiteSpace(origin)) return false;
                            if (corsExtraOrigins.Contains(origin.Trim())) return true;
                            return IsAllowedDevOrigin(origin);
                        })
                        .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "x-signalr-user-agent", "X-Api-Key")
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .AllowCredentials();
                });
            });

            // 11. JWT Authentication
            var jwtSecret =
                (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"])
                    ? null
                    : builder.Configuration["Jwt:Secret"])
                ?? Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? throw new InvalidOperationException("JWT secret is not configured. Set Jwt:Secret in appsettings.json or JWT_SECRET env var.");
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TicketSystem";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TicketSystemClients";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

            const string smartAuthScheme = "SmartAuth";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = smartAuthScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(smartAuthScheme, smartAuthScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var auth = context.Request.Headers.Authorization.FirstOrDefault();
                    if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = auth["Bearer ".Length..].Trim();
                        if (token.StartsWith("ts_", StringComparison.OrdinalIgnoreCase))
                            return StaffApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    if (!string.IsNullOrEmpty(context.Request.Headers["X-Api-Key"]))
                        return StaffApiKeyAuthenticationDefaults.AuthenticationScheme;

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(options =>
            {
                // Стандартный маппинг клеймов JWT → ClaimTypes.Role; не задавать RoleClaimType="role",
                // если в токене только длинный URI (иначе IsInRole и политики дают 403).
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromMinutes(2),
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, StaffApiKeyAuthenticationHandler>(
                StaffApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });

            builder.Services.AddAuthorization(options =>
            {
                // Сотрудники: есть хотя бы одна роль не client (без IsInRole — только явный перебор клеймов)
                options.AddPolicy("StaffOnly", policy =>
                    policy.RequireAssertion(ctx =>
                    {
                        if (!(ctx.User.Identity?.IsAuthenticated ?? false))
                            return false;

                        static IEnumerable<string> RoleClaimValues(ClaimsPrincipal u)
                        {
                            foreach (var c in u.Claims)
                            {
                                var t = c.Type;
                                var isRoleClaim =
                                    t == ClaimTypes.Role
                                    || string.Equals(t, "role", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(t, "roles", StringComparison.OrdinalIgnoreCase)
                                    || t.EndsWith("/role", StringComparison.OrdinalIgnoreCase)
                                    || t.EndsWith("/roles", StringComparison.OrdinalIgnoreCase);
                                if (!isRoleClaim) continue;

                                var v = (c.Value ?? "").Trim();
                                if (v.Length == 0) continue;
                                foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                {
                                    if (part.Length > 0) yield return part;
                                }
                            }
                        }

                        return RoleClaimValues(ctx.User)
                            .Any(v => !string.Equals(v, "client", StringComparison.OrdinalIgnoreCase));
                    }));
            });

            var app = builder.Build();

            // Seed database and apply migrations (skip in test environment)
            if (!app.Environment.IsEnvironment("Test"))
            {
                DbSeeder.SeedAsync(app.Services, builder.Configuration, app.Logger).GetAwaiter().GetResult();
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Ensure uploads directories exist
            var webRoot = app.Environment.WebRootPath ?? "wwwroot";
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "tickets"));
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "avatars"));
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "chat"));

            app.UseSerilogRequestLogging();
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseRateLimiter();
            app.UseCors("AllowFrontend");
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseWebSockets();

            app.MapControllers().RequireRateLimiting("fixed");
            app.MapHealthChecks("/health").RequireRateLimiting("fixed");

            app.MapGet("/uploads/{**path}", (string path, IWebHostEnvironment env) =>
            {
                // URL /uploads/tickets/1/a.png → path = "tickets/1/a.png" (без сегмента uploads)
                var safePath = path.Replace("\\", "/").TrimStart('/');
                if (safePath.Contains("..", StringComparison.Ordinal))
                    return Results.BadRequest("Invalid path.");

                var webRoot = env.WebRootPath ?? "wwwroot";
                var fullPath = Path.Combine(webRoot, "uploads", safePath);
                var uploadsRoot = Path.Combine(webRoot, "uploads");
                var resolvedFile = Path.GetFullPath(fullPath);
                var resolvedRoot = Path.GetFullPath(uploadsRoot);

                if (!resolvedFile.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    return Results.BadRequest("Invalid path.");

                if (!File.Exists(resolvedFile))
                    return Results.NotFound();

                var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(resolvedFile, out var contentType))
                    contentType = "application/octet-stream";

                var isInline = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                            || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

                if (isInline)
                    return Results.File(resolvedFile, contentType);

                return Results.File(resolvedFile, contentType, fileDownloadName: Path.GetFileName(resolvedFile));
            })
            // Браузер не шлёт Authorization на <img>/<a download>; путь содержит GUID — не перечисляемый.
            .AllowAnonymous()
            .RequireRateLimiting("fixed");

            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapHub<ChatHub>("/hubs/chat");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
