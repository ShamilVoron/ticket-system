using System.Text.Json.Serialization;

namespace ITCafe.Api.Dtos.Employees;

public record StaffDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("department")] string Department,
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("authEmail")] string AuthEmail,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl,
    [property: JsonPropertyName("workSchedule")] string WorkSchedule,
    [property: JsonPropertyName("workScheduleGridJson")] string WorkScheduleGridJson,
    [property: JsonPropertyName("permissionsJson")] string PermissionsJson
);
