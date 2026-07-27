namespace ITCafe.Api.Models;

public class ChatConversation
{
    public Guid Id { get; set; }
    public int? OrganizationId { get; set; }
    public bool IsGroup { get; set; }
    public string? Title { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }

    /// <summary>Slug отдела для каналов (#support, #engineers, …).</summary>
    public string? DepartmentSlug { get; set; }

    /// <summary>Связанная заявка для чата по тикету.</summary>
    public int? TicketId { get; set; }

    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
