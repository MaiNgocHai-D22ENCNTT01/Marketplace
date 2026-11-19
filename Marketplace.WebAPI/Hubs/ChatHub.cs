using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Marketplace.WebAPI.Hubs;

[Authorize(Roles = "CUSTOMER,SELLER")]
public sealed class ChatHub : Hub { }

public sealed class ChatUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.Identity?.Name;
}
