using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITCafe.Api.Services;

/// <summary>Рассылает клиентам событие для автообновления списков и карточки заявки (SignalR).</summary>
public class TicketRealtimeBroadcaster(
    IHubContext<NotificationHub> hub,
    ILogger<TicketRealtimeBroadcaster> logger)
{
    private const string EventName = "TicketSync";

    public async Task NotifyTicketChangedAsync(
        int? ticketId = null,
        string kind = "generic",
        string? actorUserId = null,
        string? message = null,
        IReadOnlyList<string>? recipientUserIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new TicketSyncPayload
            {
                TicketId = ticketId,
                Kind = string.IsNullOrWhiteSpace(kind) ? "generic" : kind,
                ActorUserId = actorUserId,
                Message = message,
                RecipientUserIds = recipientUserIds?.Count > 0 ? recipientUserIds.ToList() : null,
            };

            // Отправляем только целевым пользователям, если они указаны;
            // иначе — всем подключённым (для generic-обновлений списков).
            if (recipientUserIds is { Count: > 0 })
            {
                var tasks = new List<Task>(recipientUserIds.Count);
                foreach (var uid in recipientUserIds.Distinct(StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(uid)) continue;
                    tasks.Add(hub.Clients.User(uid.Trim()).SendAsync(EventName, payload, cancellationToken));
                }
                await Task.WhenAll(tasks);
            }
            else
            {
                await hub.Clients.All.SendAsync(EventName, payload, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR {Event} failed (ticketId={TicketId})", EventName, ticketId);
        }
    }
}
