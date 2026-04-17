using System.Security.Claims;

namespace ITCafe.Api.Extensions;

/// <summary>
/// Общие расширения для извлечения данных из JWT-клеймов.
/// Устраняет дублирование CurrentUserId/CurrentUserRole/IsStaff в контроллерах и сервисах.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal? user) =>
        user?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user?.FindFirstValue("sub");

    public static string GetRequiredUserId(this ClaimsPrincipal? user) =>
        user.GetUserId()
        ?? throw new InvalidOperationException("User identifier not found");

    public static string GetUserName(this ClaimsPrincipal? user) =>
        user?.FindFirstValue("fullName")
        ?? user?.FindFirstValue(ClaimTypes.Name)
        ?? user?.Identity?.Name
        ?? "Unknown";

    public static string GetUserRole(this ClaimsPrincipal? user) =>
        user?.FindFirstValue(ClaimTypes.Role)
        ?? user?.FindFirstValue("role")
        ?? "client";

    /// <summary>
    /// Пользователь — не клиент (учитывает все role-claims в JWT).
    /// </summary>
    public static bool IsStaffUser(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;

        var roles = user.Claims
            .Where(c =>
                c.Type == ClaimTypes.Role
                || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "Role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
            return !string.Equals(user.GetUserRole(), "client", StringComparison.OrdinalIgnoreCase);

        return roles.Any(r => !string.Equals(r, "client", StringComparison.OrdinalIgnoreCase));
    }
}
