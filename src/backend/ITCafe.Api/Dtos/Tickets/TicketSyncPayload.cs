namespace ITCafe.Api.Dtos.Tickets;

/// <summary>Пейлоад SignalR TicketSync для UI и браузерных уведомлений.</summary>
public class TicketSyncPayload
{
    public int? TicketId { get; set; }
    public string Kind { get; set; } = "generic";
    public string? ActorUserId { get; set; }
    public string? Message { get; set; }
    public List<string>? RecipientUserIds { get; set; }
}
