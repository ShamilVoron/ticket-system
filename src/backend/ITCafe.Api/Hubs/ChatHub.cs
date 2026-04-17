using System.Security.Claims;
using ITCafe.Api.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ITCafe.Api.Hubs;

[Authorize(Policy = "StaffOnly")]
public class ChatHub(IMessengerService messenger) : Hub
{
    public const string GroupPrefix = "chat:";

    public static string RoomName(Guid conversationId) => $"{GroupPrefix}{conversationId}";

    public async Task JoinConversation(string conversationId)
    {
        var userId = GetUserId();
        if (userId == null || !Guid.TryParse(conversationId, out var cid)) return;
        if (!await messenger.UserIsMemberAsync(cid, userId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomName(cid));
    }

    public async Task LeaveConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomName(cid));
    }

    private string? GetUserId() =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue("sub");
}
