using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ITCafe.Api.SignalR;

/// <summary>Маршрутизация Clients.User(userId) по claim субъекта JWT (совпадает с UserAccount.UserId).</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? connection.User?.FindFirstValue("sub");
}
