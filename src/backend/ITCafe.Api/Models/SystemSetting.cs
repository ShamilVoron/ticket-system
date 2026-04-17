using System.ComponentModel.DataAnnotations;

namespace ITCafe.Api.Models;

public class SystemSetting
{
    [Key]
    [Required]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
