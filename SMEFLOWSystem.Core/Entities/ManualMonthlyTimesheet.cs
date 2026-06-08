using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Domain Entity: Bảng công nhập tay hàng tháng (Manual Monthly Timesheet).
/// Cho phép HR nhập tay ngày công cho nhân viên khi không dùng chấm công tự động.
/// </summary>
public class ManualMonthlyTimesheet : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int ActualWorkingDays { get; set; }
    public int AbsentDays { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEarlyLeaveMinutes { get; set; }
    public decimal TotalOTHours { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
