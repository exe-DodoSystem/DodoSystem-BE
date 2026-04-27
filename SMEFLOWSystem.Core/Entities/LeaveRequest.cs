using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Domain Entity: Đơn xin nghỉ phép (Nửa ngày / Cả ngày).
/// </summary>
public partial class LeaveRequest : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
        
    public Guid EmployeeId { get; private set; }
    public string LeaveType { get; private set; } = string.Empty; // Phép năm, Việc riêng...
    public string Status { get; private set; } = "Pending"; // Pending, Approved, Rejected
    
    // Mapping Nghỉ Phép với Giai Đoạn (Segment) cụ thể. Cấm xin nguyên ngày sáo rỗng!
    public virtual ICollection<LeaveRequestSegment> Segments { get; private set; } = new List<LeaveRequestSegment>();

    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    protected LeaveRequest() { }

    public LeaveRequest(Guid tenantId, Guid employeeId, string leaveType)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EmployeeId = employeeId;
        LeaveType = leaveType;
        Status = "Pending";
    }

    /// <summary>
    /// Hàm duyệt đơn (Chỉ được duyệt khi Pending)
    /// </summary>
    public void Approve(Guid approverId)
    {
        if (Status != "Pending")
            throw new InvalidOperationException("Chỉ duyệt đơn đang trong trạng thái chờ.");
            
        Status = "Approved";
        ApprovedByUserId = approverId;
        ApprovedAt = DateTime.UtcNow;
    }
}

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
