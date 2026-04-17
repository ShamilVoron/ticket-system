namespace ITCafe.Api.Dtos.Employees;

public record EmployeeDto(
    int Id,
    string UserId,
    string FullName,
    string Role,
    string Department,
    string Login,
    string Email,
    string AvatarUrl,
    string WorkSchedule,
    string WorkScheduleGridJson,
    string PermissionsJson,
    string TelegramChatId,
    int? OkdeskId
);
