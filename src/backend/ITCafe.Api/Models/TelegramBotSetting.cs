using System.ComponentModel.DataAnnotations;

namespace ITCafe.Api.Models;

public class TelegramBotSetting
{
    public int Id { get; set; }
    [Required]
    public string EventType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string ChatId { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public int? AlertThresholdMinutes { get; set; }
    public string TargetType { get; set; } = "chat"; // chat | assignee | reporter | employee
    public string? TargetEmployeeId { get; set; }
}
