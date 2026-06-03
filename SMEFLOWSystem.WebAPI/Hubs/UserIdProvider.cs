using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SMEFLOWSystem.WebAPI.Hubs;

public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
