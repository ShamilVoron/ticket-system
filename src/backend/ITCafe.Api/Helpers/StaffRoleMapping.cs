namespace ITCafe.Api.Helpers;

/// <summary>
/// Подпись <see cref="Models.Employee.Role"/> (русский заголовок) → slug <see cref="Models.UserAccount.Role"/>.
/// Нужен при рассинхроне: в UserAccounts остался client/мусор, в Employees — корректная должность.
/// </summary>
public static class StaffRoleMapping
{
    public static string? SlugFromEmployeeRoleTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var t = title.Trim();
        return t switch
        {
            "Сапорт 1 линия" => "support_l1",
            "Сапорт 2 линия" => "support_l2",
            "Разработчик" => "developer",
            "Выездной инженер" => "field_engineer",
            "Бухгалтерия" => "accountant",
            "Нач. отдела инженеров" => "head_engineers",
            "Нач. отдела сапорта" => "head_support",
            "Нач. отдела разработки" => "head_dev",
            "Системный администратор" => "sysadmin",
            "Координатор" => "coordinator",
            "Директор" => "director",
            "Супер-админ" => "super_admin",
            "Закупки / Внеш." => "procurement",
            "Нач. отдела ремонта" => "head_repair",
            "Агент" => "agent",
            _ => null
        };
    }

    /// <summary>
    /// Точное совпадение + эвристика по «выездн…инжен…» (лишние пробелы, «Выездные инженеры» в подотделе).
    /// </summary>
    public static string? SlugFromLooseEmployeeText(string? roleTitle, string? department = null)
    {
        foreach (var raw in new[] { roleTitle, department })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var exact = SlugFromEmployeeRoleTitle(raw);
            if (exact != null) return exact;
            var t = raw.Trim();
            if (LooksLikeFieldEngineer(t)) return "field_engineer";
        }

        return null;
    }

    private static bool LooksLikeFieldEngineer(string t) =>
        t.Contains("выездн", StringComparison.OrdinalIgnoreCase)
        && t.Contains("инжен", StringComparison.OrdinalIgnoreCase);
}
