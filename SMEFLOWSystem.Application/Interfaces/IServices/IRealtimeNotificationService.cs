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

    /// <summary>
    /// Gửi tín hiệu refresh dashboard tới toàn tenant.
    /// </summary>
    Task NotifyDashboardRefreshAsync(Guid tenantId);

    /// <summary>
    /// Gửi khi nhân viên submit đơn giải trình. Gửi tới TenantAdmin + HRManager.
    /// </summary>
    Task NotifyAppealSubmittedAsync(Guid tenantId, object data);

    /// <summary>
    /// Gửi khi lương được đánh dấu đã thanh toán.
    /// </summary>
    Task NotifyPayrollPaidAsync(Guid userId, object data);

    /// <summary>
    /// Gửi khi lịch ca mới được gán cho nhân viên.
    /// </summary>
    Task NotifyShiftAssignedAsync(Guid userId, object data);

    /// <summary>
    /// Gửi khi có khoản thưởng/phạt mới được thêm.
    /// </summary>
    Task NotifyBonusDeductionEntryAddedAsync(Guid userId, object data);

    /// <summary>
    /// Gửi khi HR điều chỉnh chấm công thủ công.
    /// </summary>
    Task NotifyAttendanceManualAdjustedAsync(Guid userId, object data);

    /// <summary>
    /// Gửi khi admin hoàn thành generate/recalculate bảng lương.
    /// </summary>
    Task NotifyPayrollGeneratedAsync(Guid userId, object data);

    /// <summary>
    /// Gửi khi nhân viên hoàn thành onboarding. Gửi tới TenantAdmin + HRManager.
    /// </summary>
    Task NotifyEmployeeOnboardedAsync(Guid tenantId, object data);
}
