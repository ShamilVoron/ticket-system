using System.Security.Claims;
using System.Text.Encodings.Web;
using ITCafe.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITCafe.Api.Authentication;

/// <summary>
/// Аутентификация интеграций: заголовок X-Api-Key или Bearer ts_…
/// Ключ хранится как BCrypt-хэш в SystemSettings, привязка к UserId сотрудника.
/// </summary>
public class StaffApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            var auth = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(auth)
                && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = auth["Bearer ".Length..].Trim();
                if (token.StartsWith("ts_", StringComparison.Ordinal))
                    raw = token;
            }
        }

        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hashRow = await db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.HashSettingKey);
        var userRow = await db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.UserIdSettingKey);

        if (hashRow == null || userRow == null || string.IsNullOrWhiteSpace(userRow.Value))
            return AuthenticateResult.Fail("API key not configured");

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(raw, hashRow.Value))
                return AuthenticateResult.Fail("Invalid API key");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Staff API key verification failed");
            return AuthenticateResult.Fail("Invalid API key");
        }

        var account = await db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userRow.Value);
        if (account == null || string.Equals(account.Role, "client", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Invalid API key user");

        var r = (account.Role ?? string.Empty).Trim();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserId),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, r),
            new("role", r),
            new("fullName", account.FullName),
        };
        var id = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name));
    }
}
