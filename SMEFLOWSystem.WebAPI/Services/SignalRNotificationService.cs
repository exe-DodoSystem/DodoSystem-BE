using Microsoft.AspNetCore.SignalR;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.WebAPI.Hubs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.WebAPI.Services;

public class SignalRNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyAttendanceUpdatedAsync(Guid userId, Guid tenantId, object data)
    {
        try
        {
            // Gửi cho nhân viên: trạng thái chấm công hôm nay đã cập nhật
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("attendance.updated", data);

            // Gửi cho toàn bộ user trong tenant: yêu cầu refresh dashboard
            await _hub.Clients.Group($"tenant:{tenantId}:dashboard")
                .SendAsync("dashboard.refresh", new { tenantId });
        }
        catch (Exception ex)
        {
            // Không throw — notify là best-effort, không được ảnh hưởng flow chính
            _logger.LogWarning(ex, "Failed to notify attendance updated for user {UserId}", userId);
        }
    }

    public async Task NotifyAppealProcessedAsync(Guid userId, object data)
    {
        try
        {
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("appeal.processed", data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify appeal processed for user {UserId}", userId);
        }
    }

    public async Task NotifyPayrollPublishedAsync(Guid userId, object data)
    {
        try
        {
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("payroll.published", data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify payroll published for user {UserId}", userId);
        }
    }

    public async Task NotifyPunchReceivedAsync(Guid userId, object data)
    {
        try
        {
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("punch.received", data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify punch received for user {UserId}", userId);
        }
    }
}
