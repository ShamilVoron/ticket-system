using System.ComponentModel.DataAnnotations;

namespace ITCafe.Api.Models;

public class SystemStatus
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string RoleFilter { get; set; } = string.Empty; // JSON array of roles
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
