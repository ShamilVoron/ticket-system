using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

// Relational model for employee profile (used by the Employees page).
public class Employee
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string WorkSchedule { get; set; } = string.Empty;
    public string WorkScheduleGridJson { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime PasswordUpdatedAt { get; set; } = DateTime.UtcNow;
    public int? OkdeskId { get; set; }
    public string PermissionsJson { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
}
