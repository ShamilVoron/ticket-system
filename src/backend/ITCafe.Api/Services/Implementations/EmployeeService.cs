using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Employees;
using ITCafe.Api.Helpers;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _context;

    private static readonly HashSet<string> CreatableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "support_l1", "support_l2", "developer", "field_engineer", "accountant",
        "head_engineers", "head_support", "head_dev", "sysadmin",
        "coordinator", "director", "super_admin", "procurement", "head_repair",
        "agent"
    };

    public EmployeeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<StaffDto>> GetAllStaffAsync()
    {
        var rows = await _context.UserAccounts
            .Where(u => u.Role != "client")
            .GroupJoin(
                _context.Employees,
                u => u.UserId,
                e => e.UserId,
                (u, emp) => new { User = u, Emp = emp.FirstOrDefault() })
            .Select(x => new
            {
                x.User.UserId,
                x.User.FullName,
                x.User.Role,
                Department = x.Emp != null ? (x.Emp.Department ?? string.Empty) : string.Empty,
                EmpLogin = x.Emp != null ? x.Emp.Login : null,
                EmpEmail = x.Emp != null ? x.Emp.Email : null,
                UserEmail = x.User.Email,
                AvatarUrl = x.Emp != null ? (x.Emp.AvatarUrl ?? string.Empty) : string.Empty,
                WorkSchedule = x.Emp != null ? (x.Emp.WorkSchedule ?? string.Empty) : string.Empty,
                WorkScheduleGridJson = x.Emp != null ? (x.Emp.WorkScheduleGridJson ?? string.Empty) : string.Empty,
                PermissionsJson = x.Emp != null ? (x.Emp.PermissionsJson ?? string.Empty) : string.Empty
            })
            .ToListAsync();

        return rows.Select(x => new StaffDto(
            x.UserId,
            x.FullName,
            x.Role,
            x.Department,
            DeriveStaffLogin(x.EmpLogin, x.EmpEmail, x.UserEmail),
            x.UserEmail ?? string.Empty,
            x.AvatarUrl,
            x.WorkSchedule,
            x.WorkScheduleGridJson,
            x.PermissionsJson)).ToList();
    }

    public async Task<EmployeeDto?> GetEmployeeAsync(string userId)
    {
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (emp == null) return null;

        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
        var loginDisplay = DeriveStaffLogin(emp.Login, emp.Email, account?.Email ?? string.Empty);
        // Для формы редактирования нужен slug роли (как в списке сотрудников), а не подпись из Employees.Role
        var roleSlug = !string.IsNullOrWhiteSpace(account?.Role)
            ? account!.Role.Trim().ToLowerInvariant()
            : RoleSlugFromEmployeeRoleTitle(emp.Role);

        return new EmployeeDto(
            emp.Id,
            emp.UserId,
            emp.FullName,
            roleSlug,
            emp.Department ?? string.Empty,
            loginDisplay,
            emp.Email,
            emp.AvatarUrl,
            emp.WorkSchedule ?? string.Empty,
            emp.WorkScheduleGridJson ?? string.Empty,
            emp.PermissionsJson ?? string.Empty,
            emp.TelegramChatId ?? string.Empty,
            emp.OkdeskId
        );
    }

    public async Task<bool> ChangeLoginAsync(string userId, string newLogin)
    {
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (emp == null) return false;

        emp.Login = newLogin;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
    {
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (emp == null) return false;

        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
        if (account == null) return false;

        if (account.Password.StartsWith("$2") && !BCrypt.Net.BCrypt.Verify(oldPassword, account.Password))
            return false;

        if (!account.Password.StartsWith("$2") && account.Password != oldPassword)
            return false;

        account.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        emp.PasswordUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeScheduleAsync(string userId, string? newSchedule, string? newScheduleGridJson)
    {
        var emp = await GetOrCreateEmployeeForStaffAsync(userId);
        if (emp == null) return false;

        if (newSchedule != null)
            emp.WorkSchedule = newSchedule.Trim();
        if (newScheduleGridJson != null)
            emp.WorkScheduleGridJson = newScheduleGridJson.Trim();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> ChangeAvatarAsync(string userId, IFormFile file, string webRootPath)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File not selected");

        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (emp == null) throw new KeyNotFoundException("Employee not found");

        var uploadsPath = Path.Combine(webRootPath, "uploads", "avatars");
        if (!Directory.Exists(uploadsPath))
            Directory.CreateDirectory(uploadsPath);

        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("File too large");

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".webp")
            throw new ArgumentException("Invalid file type");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        if (!await FileSignatureValidator.IsValidAsync(ms, ext))
            throw new ArgumentException("File content does not match the declared extension.");
        ms.Position = 0;

        var fileName = $"{userId}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await ms.CopyToAsync(stream);
        }

        emp.AvatarUrl = $"/uploads/avatars/{fileName}";
        await _context.SaveChangesAsync();

        return emp.AvatarUrl;
    }

    public async Task<IActionResult> UpdateProfileAsync(string userId, UpdateEmployeeProfileDto dto)
    {
        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
        if (account == null) return new NotFoundObjectResult("User account not found.");

        var emp = await GetOrCreateEmployeeForStaffAsync(userId);
        if (emp == null) return new NotFoundObjectResult("Employee record not found.");

        if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            var name = dto.FullName.Trim();
            account.FullName = name;
            emp.FullName = name;
        }

        if (!string.IsNullOrWhiteSpace(dto.Role))
        {
            var role = NormalizeCreatableRoleSlug(dto.Role.Trim().ToLowerInvariant());
            if (!CreatableRoles.Contains(role))
                return new BadRequestObjectResult($"Invalid role: \"{role}\".");
            account.Role = role;
            emp.Role = EmployeeRoleTitle(role);
        }

        if (dto.Department != null)
            emp.Department = dto.Department.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Login))
        {
            var login = dto.Login.Trim();
            emp.Login = login;
            var emailPart = login.Contains('@') ? login : $"{login}@local";
            var emailConflict = await _context.UserAccounts
                .AnyAsync(u => u.Email == emailPart && u.UserId != userId);
            if (emailConflict)
                return new ConflictObjectResult($"Login \"{login}\" is already taken.");
            account.Email = emailPart;
        }

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            account.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            emp.PasswordUpdatedAt = DateTime.UtcNow;
        }

        if (dto.PermissionsJson != null)
            emp.PermissionsJson = dto.PermissionsJson.Trim();

        if (dto.TelegramChatId != null)
            emp.TelegramChatId = dto.TelegramChatId.Trim();

        if (dto.OkdeskId.HasValue)
            emp.OkdeskId = dto.OkdeskId.Value;
        else if (dto.OkdeskId == null && emp.OkdeskId.HasValue)
            emp.OkdeskId = null;

        await _context.SaveChangesAsync();
        return new OkObjectResult(new { message = "Profile updated." });
    }

    public async Task<bool> DeleteEmployeeAsync(string userId)
    {
        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);

        if (account == null && emp == null)
            return false;

        if (emp != null) _context.Employees.Remove(emp);
        if (account != null) _context.UserAccounts.Remove(account);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<object> CreateAccountAsync(CreateEmployeeAccountDto dto)
    {
        var role = NormalizeCreatableRoleSlug((dto.Role ?? string.Empty).Trim().ToLowerInvariant());
        if (string.IsNullOrEmpty(role))
            throw new ArgumentException("Role is required.");
        if (!CreatableRoles.Contains(role))
            throw new ArgumentException($"Invalid role: \"{role}\".");

        var login = (dto.Login ?? string.Empty).Trim();
        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("fullName and password are required.");
        if (string.IsNullOrWhiteSpace(login) && string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Either login or email must be provided.");
        if (string.IsNullOrWhiteSpace(login))
            login = email.Split('@')[0];

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailExists = await _context.UserAccounts.AnyAsync(u => u.Email == email);
            if (emailExists)
                throw new InvalidOperationException("Email already registered.");
        }

        var userId = $"user-{Guid.NewGuid().ToString("N")[..8]}";
        var effectiveEmail = string.IsNullOrWhiteSpace(email) ? $"{login}@local" : email;

        var effectiveEmailExists = await _context.UserAccounts.AnyAsync(u => u.Email == effectiveEmail);
        if (effectiveEmailExists)
            throw new InvalidOperationException($"Login \"{login}\" is already taken.");

        var account = new UserAccount
        {
            UserId = userId,
            FullName = dto.FullName.Trim(),
            Email = effectiveEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role
        };

        var employee = new Employee
        {
            UserId = userId,
            FullName = account.FullName,
            Role = EmployeeRoleTitle(role),
            Department = (dto.Department ?? string.Empty).Trim(),
            Login = login,
            Email = string.IsNullOrWhiteSpace(email) ? string.Empty : email,
            AvatarUrl = "",
            WorkSchedule = string.Empty,
            WorkScheduleGridJson = string.Empty
        };

        _context.UserAccounts.Add(account);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        return new
        {
            userId = account.UserId,
            fullName = account.FullName,
            email = account.Email,
            role = account.Role,
            department = employee.Department
        };
    }

    private async Task<Employee?> GetOrCreateEmployeeForStaffAsync(string userId)
    {
        var existing = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (existing != null)
            return existing;

        var acc = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
        if (acc == null || string.Equals(acc.Role, "client", StringComparison.OrdinalIgnoreCase))
            return null;

        var email = acc.Email ?? string.Empty;
        var emp = new Employee
        {
            UserId = acc.UserId,
            FullName = acc.FullName,
            Role = EmployeeRoleTitle(acc.Role),
            Login = DeriveStaffLogin(null, null, email),
            Email = email,
            AvatarUrl = "https://ui-avatars.com/api/?name=Admin&background=random",
            WorkSchedule = string.Empty,
            WorkScheduleGridJson = string.Empty
        };
        _context.Employees.Add(emp);
        return emp;
    }

    private static string NormalizeCreatableRoleSlug(string role)
    {
        if (string.IsNullOrEmpty(role)) return role;
        if (string.Equals(role, "support", StringComparison.OrdinalIgnoreCase)) return "support_l1";
        return role;
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
        "agent" => "Агент",
        _ => "Сотрудник"
    };

    /// <summary>Обратное соответствие подписи Employees.Role → slug UserAccounts.Role для старых данных.</summary>
    private static string RoleSlugFromEmployeeRoleTitle(string? title) =>
        StaffRoleMapping.SlugFromEmployeeRoleTitle(title) ?? string.Empty;

    private static string DeriveStaffLogin(string? employeeLogin, string? employeeEmail, string userAccountEmail)
    {
        if (!string.IsNullOrWhiteSpace(employeeLogin)) return employeeLogin.Trim();
        var email = !string.IsNullOrWhiteSpace(employeeEmail) ? employeeEmail : userAccountEmail;
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var i = email.IndexOf('@');
        return i > 0 ? email[..i].Trim() : email.Trim();
    }
}
