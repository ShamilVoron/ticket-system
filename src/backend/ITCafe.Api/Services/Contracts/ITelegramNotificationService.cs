using ITCafe.Api.Models;

namespace ITCafe.Api.Services.Contracts;

public interface ITelegramNotificationService
{
    Task NotifyNewTicketAsync(Ticket ticket);
    Task NotifyStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus);
    Task NotifyFieldReportAddedAsync(Ticket ticket, FieldReport report);
    Task NotifySubtaskCreatedAsync(Ticket ticket, TicketSubtask subtask);
    Task NotifyAssigneeChangedAsync(Ticket ticket, string? oldAssigneeUserId = null);
    /// <summary>Send Telegram for a custom event type (e.g. sla_80, sla_breach, automation).</summary>
    Task NotifyEventAsync(Ticket ticket, string eventType, Dictionary<string, string>? extra = null);
    Task<bool> TestTokenAsync(string token);
}
