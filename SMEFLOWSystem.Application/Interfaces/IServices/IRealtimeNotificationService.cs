using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IRealtimeNotificationService
{
    /// <summary>
    /// Gọi sau khi Background Job xử lý xong DailyTimesheet cho 1 nhân viên.
    /// Gửi cho nhân viên đó + tín hiệu refresh dashboard tới toàn tenant.
    /// </summary>
    Task NotifyAttendanceUpdatedAsync(Guid userId, Guid tenantId, object data);

    /// <summary>
    /// Gọi sau khi HR approve hoặc reject appeal của nhân viên.
    /// </summary>
    Task NotifyAppealProcessedAsync(Guid userId, object data);

    /// <summary>
    /// Gọi sau khi admin publish phiếu lương.
    /// </summary>
    Task NotifyPayrollPublishedAsync(Guid userId, object data);

    /// <summary>
    /// Gọi ngay sau khi nhận POST /submit-punch thành công.
    /// Xác nhận server đã nhận, đang chờ job xử lý.
    /// </summary>
    Task NotifyPunchReceivedAsync(Guid userId, object data);
}
