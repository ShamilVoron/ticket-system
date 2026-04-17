namespace ITCafe.Api.Models;

public class ChatMember
{
    public Guid ConversationId { get; set; }
    public ChatConversation Conversation { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAtUtc { get; set; }
}
