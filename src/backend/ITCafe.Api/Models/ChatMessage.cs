namespace ITCafe.Api.Models;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ChatConversation Conversation { get; set; } = null!;
    public string SenderUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Относительный URL вида uploads/chat/{conversationId}/… (в ответе API — с ведущим /).</summary>
    public string? AttachmentUrl { get; set; }
    public string? AttachmentMimeType { get; set; }
    public string? AttachmentFileName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>JSON-массив реакций: [{"emoji":"🐧","userId":"...","userName":"..."}]</summary>
    public string? ReactionsJson { get; set; }
}
