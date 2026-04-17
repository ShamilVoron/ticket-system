namespace ITCafe.Api.Models;

public class UserTicketReadState
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int TicketId { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
}
