using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

public partial class LeaveRequestSegment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeaveRequestId { get; private set; }
    
    public DateOnly LeaveDate { get; private set; }
    
    // Cốt lõi của bài toán "Nửa ngày phép đụng Ca Gãy"
    // Nếu xin nửa ngày, ID này sẽ link thẳng vào Segment tương ứng của Ca làm việc.
    public Guid TargetShiftSegmentId { get; private set; } 
    
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
