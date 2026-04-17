namespace ITCafe.Api.Dtos.Employees;

public record UpdateEmployeeProfileDto(
    string? FullName = null,
    string? Role = null,
    string? Department = null,
    string? Login = null,
    string? Password = null,
    string? PermissionsJson = null,
    string? TelegramChatId = null,
    int? OkdeskId = null
);
