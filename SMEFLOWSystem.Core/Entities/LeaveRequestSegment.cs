using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Chi tiết những ngày/buổi xin nghỉ.
/// </summary>
public partial class LeaveRequestSegment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeaveRequestId { get; private set; }
    
    /// <summary>Ngày xin nghỉ.</summary>
    public DateOnly LeaveDate { get; private set; }
    
    // Cốt lõi của bài toán "Nửa ngày phép đụng Ca Gãy"
    // Nếu xin nửa ngày, ID này sẽ link thẳng vào Segment tương ứng của Ca làm việc.
    public Guid TargetShiftSegmentId { get; private set; } 
    
    /// <summary>Số giờ xin nghỉ.</summary>
    public decimal HoursRequested { get; private set; }
    
    public virtual LeaveRequest? LeaveRequest { get; set; }
    public virtual ShiftSegment? TargetShiftSegment { get; set; }

    protected LeaveRequestSegment() { }

    public LeaveRequestSegment(Guid tenantId, Guid leaveRequestId, DateOnly date, Guid targetSegmentId, decimal hours)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        LeaveRequestId = leaveRequestId;
        LeaveDate = date;
        TargetShiftSegmentId = targetSegmentId;
        HoursRequested = hours;
    }
}
