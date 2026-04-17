using ITCafe.Api.Dtos.Messenger;
using ITCafe.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITCafe.Api.Services;

public class ChatRealtimeBroadcaster(
    IHubContext<ChatHub> hub,
    ILogger<ChatRealtimeBroadcaster> logger)
{
    /// <summary>
    /// Доставляет сообщение всем участникам беседы по UserId (не только вошедшим в группу комнаты),
    /// чтобы клиенты в том числе шапки/уведомлений получали ChatMessage.
    /// </summary>
    public async Task BroadcastChatMessageAsync(
        Guid conversationId,
        ChatMessageDto dto,
        IReadOnlyList<string> memberUserIds,
        CancellationToken ct = default)
    {
        try
        {
            foreach (var uid in memberUserIds.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;
                await hub.Clients.User(uid.Trim()).SendAsync("ChatMessage", dto, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ChatMessage SignalR failed for {ConversationId}", conversationId);
        }
    }

    public async Task NotifySidebarAsync(IEnumerable<string> userIds, ChatSidebarSyncDto payload, CancellationToken ct = default)
    {
        try
        {
            foreach (var uid in userIds.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;
                await hub.Clients.User(uid.Trim()).SendAsync("MessengerSidebar", payload, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MessengerSidebar SignalR failed");
        }
    }

    public async Task BroadcastChatMessageUpdatedAsync(
        Guid conversationId,
        ChatMessageDto dto,
        IReadOnlyList<string> memberUserIds,
        CancellationToken ct = default)
    {
        try
        {
            foreach (var uid in memberUserIds.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;
                await hub.Clients.User(uid.Trim()).SendAsync("ChatMessageUpdated", dto, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ChatMessageUpdated SignalR failed for {ConversationId}", conversationId);
        }
    }

    public async Task BroadcastChatMessageDeletedAsync(
        Guid conversationId,
        Guid messageId,
        IReadOnlyList<string> memberUserIds,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { conversationId, messageId };
            foreach (var uid in memberUserIds.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;
                await hub.Clients.User(uid.Trim()).SendAsync("ChatMessageDeleted", payload, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ChatMessageDeleted SignalR failed for {ConversationId}", conversationId);
        }
    }

    public async Task NotifyConversationMetaAsync(
        IEnumerable<string> userIds,
        ChatConversationDetailDto detail,
        CancellationToken ct = default)
    {
        try
        {
            foreach (var uid in userIds.Distinct())
                await hub.Clients.User(uid).SendAsync("ChatConversationUpdated", detail, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ChatConversationUpdated SignalR failed");
        }
    }
}
