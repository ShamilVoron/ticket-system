namespace ITCafe.Api.Models;

public class ChatConversation
{
    public Guid Id { get; set; }
    public bool IsGroup { get; set; }
    public string? Title { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }

    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
