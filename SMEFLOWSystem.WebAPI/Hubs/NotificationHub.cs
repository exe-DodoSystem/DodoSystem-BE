using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SMEFLOWSystem.WebAPI.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;

        if (userId != null)
        {
            // Group riêng cho từng user (nhiều tab/device cùng 1 người)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogDebug("User {UserId} connected to NotificationHub", userId);
        }

        if (tenantId != null)
        {
            // Group cho toàn bộ user trong tenant (nhận tín hiệu refresh dashboard)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:dashboard");

            // Group admin/hrmanager để nhận alert tổng hợp
            var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>();
            foreach (var role in roles)
            {
                if (role is "TenantAdmin" or "HRManager")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:admins");
                    break;
                }
                if (role == "Manager")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:managers");
                    break;
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
