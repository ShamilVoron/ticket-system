using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        // Seed default organization (single-tenant Phase Beta)
        if (!db.Organizations.Any())
        {
            db.Organizations.Add(new Organization
            {
                Id = 1,
                Name = "Default",
                Slug = "default",
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default Organization Id=1.");
        }

        // Seed default SLA policies if none exist
        if (!db.SlaPolicies.Any())
        {
            db.SlaPolicies.AddRange(
                new SlaPolicy { Priority = "*", RequestType = "*", Department = "*", ClientCategory = "*", ReactionMinutes = 60, ResolutionMinutes = 240, IsActive = true },
                new SlaPolicy { Priority = "Критический", RequestType = "*", Department = "*", ClientCategory = "*", ReactionMinutes = 15, ResolutionMinutes = 60, IsActive = true },
                new SlaPolicy { Priority = "Высокий", RequestType = "*", Department = "*", ClientCategory = "*", ReactionMinutes = 30, ResolutionMinutes = 120, IsActive = true },
                new SlaPolicy { Priority = "Низкий", RequestType = "*", Department = "*", ClientCategory = "*", ReactionMinutes = 120, ResolutionMinutes = 480, IsActive = true }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default SLA policies.");
        }

        // Seed default system statuses if none exist
        if (!db.SystemStatuses.Any())
        {
            db.SystemStatuses.AddRange(
                new SystemStatus { Name = "Открыт", ColorClass = "bg-blue-100 text-blue-800 border-blue-300", SortOrder = 0, IsDefault = true, IsActive = true },
                new SystemStatus { Name = "В работе", ColorClass = "bg-violet-100 text-violet-800 border-violet-300", SortOrder = 1, IsActive = true },
                new SystemStatus { Name = "Отложен", ColorClass = "bg-gray-100 text-gray-600 border-gray-300", SortOrder = 2, IsActive = true },
                new SystemStatus { Name = "Ожидание клиента", ColorClass = "bg-yellow-100 text-yellow-800 border-yellow-300", SortOrder = 3, IsActive = true },
                new SystemStatus { Name = "Требуется координатор", ColorClass = "bg-orange-100 text-orange-800 border-orange-300", SortOrder = 4, IsActive = true },
                new SystemStatus { Name = "У инженера / в ремонте", ColorClass = "bg-sky-100 text-sky-800 border-sky-300", SortOrder = 5, IsActive = true },
                new SystemStatus { Name = "Решено", ColorClass = "bg-green-100 text-green-800 border-green-300", SortOrder = 6, IsActive = true },
                new SystemStatus { Name = "Закрыт", ColorClass = "bg-gray-200 text-gray-500 border-gray-400", SortOrder = 7, IsActive = true }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default system statuses.");
        }

        var superAdminEmail = (
            configuration["SuperAdmin:Email"] ??
            Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL")
        )?.Trim().ToLowerInvariant();

        var superAdminPassword =
            configuration["SuperAdmin:Password"] ??
            Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");

        var superAdminFullName = (
            configuration["SuperAdmin:FullName"] ??
            Environment.GetEnvironmentVariable("SUPERADMIN_FULLNAME")
        )?.Trim();

        var superAdminUserId = (
            configuration["SuperAdmin:UserId"] ??
            Environment.GetEnvironmentVariable("SUPERADMIN_USERID")
        )?.Trim();

        if (!string.IsNullOrWhiteSpace(superAdminEmail) && !string.IsNullOrWhiteSpace(superAdminPassword))
        {
            var userId = string.IsNullOrWhiteSpace(superAdminUserId) ? "super-admin" : superAdminUserId;
            var fullName = string.IsNullOrWhiteSpace(superAdminFullName) ? "Super Admin" : superAdminFullName;
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(superAdminPassword);

            var existing = db.UserAccounts.FirstOrDefault(u =>
                u.UserId == userId ||
                u.Email == superAdminEmail ||
                u.Role == "super_admin");

            if (existing == null)
            {
                db.UserAccounts.Add(new UserAccount
                {
                    UserId = userId,
                    FullName = fullName,
                    Email = superAdminEmail,
                    Password = hashedPassword,
                    Role = "super_admin"
                });
            }
            else
            {
                existing.UserId = userId;
                existing.FullName = fullName;
                existing.Email = superAdminEmail;
                existing.Role = "super_admin";
                // Do NOT overwrite password to prevent resetting admin password on every restart
            }

            db.SaveChanges();
        }
        else
        {
            logger.LogWarning(
                "Super-admin account was not seeded. Set SuperAdmin:Email and SuperAdmin:Password (or SUPERADMIN_EMAIL / SUPERADMIN_PASSWORD).");
        }

        var staffAccounts = db.UserAccounts
            .Where(u => u.Role != "client")
            .ToList();

        var existingEmployeeUserIds = db.Employees
            .Select(e => e.UserId)
            .ToHashSet();

        foreach (var acc in staffAccounts)
        {
            if (!existingEmployeeUserIds.Contains(acc.UserId))
            {
                db.Employees.Add(new Employee
                {
                    UserId = acc.UserId,
                    FullName = acc.FullName,
                    Role = acc.Role switch
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
                    },
                    Login = acc.Email.Split('@')[0],
                    Email = acc.Email,
                    AvatarUrl = "https://ui-avatars.com/api/?name=Admin&background=random",
                    WorkSchedule = string.Empty,
                    WorkScheduleGridJson = string.Empty
                });
                logger.LogInformation("Auto-created Employee record for {FullName} ({UserId})", acc.FullName, acc.UserId);
            }
        }

        foreach (var emp in db.Employees.Where(e => string.IsNullOrWhiteSpace(e.Login)))
        {
            var acc = db.UserAccounts.FirstOrDefault(u => u.UserId == emp.UserId);
            var src = !string.IsNullOrWhiteSpace(emp.Email) ? emp.Email : acc?.Email;
            if (string.IsNullOrWhiteSpace(src)) continue;
            var at = src.IndexOf('@');
            emp.Login = at > 0 ? src[..at].Trim() : src.Trim();
        }

        if (!db.SystemSettings.Any(s => s.Key == "ai_provider"))
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "ai_provider",
                Value = "none",
                UpdatedAt = DateTime.UtcNow,
            });
            logger.LogInformation("Seeded default ai_provider=none.");
        }

        // Временный режим: все не-клиенты → super_admin (JWT и [Authorize(Roles=…)]).
        // Чекбоксы PermissionsJson на фронте не меняют UserAccounts.Role — только меню.
        // Выключается: убрать env / App:PromoteAllStaffToSuperAdmin и перезапустить API.
        var promoteAllStaffToSuperAdmin =
            configuration.GetValue("App:PromoteAllStaffToSuperAdmin", false)
            || string.Equals(
                Environment.GetEnvironmentVariable("PROMOTE_ALL_STAFF_TO_SUPER_ADMIN"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (promoteAllStaffToSuperAdmin)
        {
            var nonClients = db.UserAccounts.Where(u => u.Role != "client").ToList();
            foreach (var acc in nonClients)
                acc.Role = "super_admin";

            var staffIds = nonClients.Select(a => a.UserId).ToHashSet();
            foreach (var emp in db.Employees.Where(e => staffIds.Contains(e.UserId)))
                emp.Role = "Супер-админ";

            logger.LogWarning(
                "PromoteAllStaffToSuperAdmin: все сотруднические UserAccounts → super_admin, подписи Employees.Role → «Супер-админ». Отключите флаг после настройки прав.");
        }

        db.SaveChanges();
    }
}
