using ITCafe.Api.Models;

namespace ITCafe.Api.Services.Contracts;

public interface IOkdeskSyncService
{
    Task<bool> IsEnabledAsync();
    Task<bool> TestConnectionAsync();
    Task SyncTicketAsync(Ticket ticket);
    Task SyncTicketCommentAsync(Ticket ticket, TicketComment comment, string? authorUserId);
}
