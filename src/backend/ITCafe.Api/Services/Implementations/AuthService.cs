using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ITCafe.Api.Data;
using ITCafe.Api.Helpers;
using ITCafe.Api.Dtos.Auth;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ITCafe.Api.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "client",
        "support_l1", "support_l2", "developer", "field_engineer", "accountant",
        "head_engineers", "head_support", "head_dev", "sysadmin",
        "coordinator", "director", "super_admin", "procurement", "head_repair",
        "agent"
    };

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _context.UserAccounts.AnyAsync(u => u.Email == normalizedEmail))
        {
            throw new InvalidOperationException("Email already registered");
        }

        var role = "client";
        var userId = $"user-{Guid.NewGuid().ToString("N")[..8]}";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var account = new UserAccount
        {
            UserId = userId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Password = hashedPassword,
            Role = role
        };

        _context.UserAccounts.Add(account);

        if (role != "client")
        {
            _context.Employees.Add(new Employee
            {
                UserId = userId,
                FullName = account.FullName,
                Role = EmployeeRoleTitle(role),
                Login = normalizedEmail.Split('@')[0],
                Email = normalizedEmail,
                AvatarUrl = "https://ui-avatars.com/api/?name=Admin&background=random",
                WorkSchedule = string.Empty,
                WorkScheduleGridJson = string.Empty
            });
        }

        await _context.SaveChangesAsync();

        var normRole = NormalizeRole(account.Role);
        var token = GenerateJwtToken(account, normRole);
        return new AuthResponse(token, account.UserId, account.FullName, account.Email, normRole, "");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var rawInput = string.IsNullOrWhiteSpace(request.Username) ? request.Email : request.Username;
        var input = (rawInput ?? string.Empty).Trim().ToLowerInvariant();
        var pwd = request.Password;

        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Email == input);

        if (account == null && !input.Contains('@'))
        {
            var localEmail = $"{input}@local";
            account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Email == localEmail);
        }

        if (account == null)
        {
            var empByLogin = await _context.Employees
                .FirstOrDefaultAsync(e => e.Login != null && e.Login.ToLower() == input);
            if (empByLogin != null)
            {
                account = await _context.UserAccounts
                    .FirstOrDefaultAsync(u => u.UserId == empByLogin.UserId);
            }
        }

        if (account == null)
            throw new UnauthorizedAccessException();

        if (!VerifyPasswordAndMigrate(account, pwd))
            throw new UnauthorizedAccessException();

        await _context.SaveChangesAsync();

        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == account.UserId);
        var avatar = emp?.AvatarUrl ?? "";

        var role = await ResolveRoleAndSyncAccountAsync(account, emp);
        var token = GenerateJwtToken(account, role);
        return new AuthResponse(token, account.UserId, account.FullName, account.Email, role, avatar);
    }

    /// <summary>
    /// Роль для JWT: slug в UserAccounts; иначе русская подпись в UserAccounts или Employees
    /// (рассинхрон для выездных: в аккаунте client, в карточке — «Выездной инженер»).
    /// </summary>
    private async Task<string> ResolveRoleAndSyncAccountAsync(UserAccount account, Employee? emp)
    {
        var fromSlug = NormalizeRole(account.Role);
        if (fromSlug != "client")
            return fromSlug;

        var fromAccTitle = StaffRoleMapping.SlugFromLooseEmployeeText(account.Role);
        if (!string.IsNullOrEmpty(fromAccTitle))
        {
            var r = NormalizeRole(fromAccTitle);
            if (r != "client" && !string.Equals(account.Role, r, StringComparison.Ordinal))
            {
                account.Role = r;
                await _context.SaveChangesAsync();
            }
            return r;
        }

        if (emp == null)
            return "client";

        var fromEmpTitle = StaffRoleMapping.SlugFromLooseEmployeeText(emp.Role, emp.Department);
        if (string.IsNullOrEmpty(fromEmpTitle))
            return "client";

        var resolved = NormalizeRole(fromEmpTitle);
        if (resolved != "client" && !string.Equals(account.Role, resolved, StringComparison.Ordinal))
        {
            account.Role = resolved;
            await _context.SaveChangesAsync();
        }

        return resolved;
    }

    public string GenerateJwtToken(UserAccount account, string? normalizedRole = null)
    {
        var role = NormalizeRole(normalizedRole ?? account.Role);

        var secret =
            (string.IsNullOrWhiteSpace(_configuration["Jwt:Secret"])
                ? null
                : _configuration["Jwt:Secret"])
            ?? Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? throw new InvalidOperationException("JWT secret not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "ITCafe";
        var audience = _configuration["Jwt:Audience"] ?? "ITCafeClients";
        var expirationMinutes = _configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? 1440;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Два клейма роли: JwtBearer и политики по-разному мапят типы клеймов
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.UserId),
            new Claim(ClaimTypes.NameIdentifier, account.UserId),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim(ClaimTypes.Name, account.FullName),
            new Claim("fullName", account.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "client";
        // NFKC: убираем «похожие» Unicode-символы из импортов (1C/Excel), иначе роль не попадает в AllowedRoles → в JWT уходит client.
        var normalized = role.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        if (normalized == "support") return "coordinator";
        if (AllowedRoles.Contains(normalized)) return normalized;
        var underscored = normalized.Replace(' ', '_');
        if (AllowedRoles.Contains(underscored)) return underscored;
        return "client";
    }

    private static string EmployeeRoleTitle(string role) => role switch
    {
        "support_l1" => "Сапорт 1 линия",
        "support_l2" => "Сапорт 2 линия",
        "developer" => "Разработчик",
        "field_engineer" => "Выездной инженер",
        "accountant" => "Бухгалтерия",
        "head_engineers" => "Нач. отдела инженеров",
        "head_support" => "Нач. отдела сапорта",
        "head_dev" => "Нач. отдела разработки",
        "sysadmin" => "Системный администратор",
        "coordinator" => "Координатор",
        "director" => "Директор",
        "super_admin" => "Супер-админ",
        "procurement" => "Закупки / Внеш.",
        "head_repair" => "Нач. отдела ремонта",
        _ => "Сотрудник"
    };

    private static bool VerifyPasswordAndMigrate(UserAccount account, string password)
    {
        if (account.Password.StartsWith("$2"))
        {
            return BCrypt.Net.BCrypt.Verify(password, account.Password);
        }

        if (account.Password == password)
        {
            account.Password = BCrypt.Net.BCrypt.HashPassword(password);
            return true;
        }

        return false;
    }
}
