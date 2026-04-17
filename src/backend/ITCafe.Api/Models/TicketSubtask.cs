namespace ITCafe.Api.Models;

public class TicketSubtask
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "в процессе";
    public string KnowledgeableUserIds { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
