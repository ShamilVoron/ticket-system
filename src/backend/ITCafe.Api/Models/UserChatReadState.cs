namespace ITCafe.Api.Models;

public class UserChatReadState
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ConversationId { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
}
