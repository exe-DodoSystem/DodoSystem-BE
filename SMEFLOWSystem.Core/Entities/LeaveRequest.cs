using ShareKernel.Common.Enum;
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
    public Guid LeaveTypeId { get; private set; }   // FK → LeaveType (thay cho string LeaveType)
    public string? ReasonNote { get; private set; } // Lý do xin nghỉ của nhân viên
    public string? AttachmentUrl { get; private set; }

    public virtual LeaveType? LeaveTypeNavigation { get; set; }

    // Mapping Nghỉ Phép với Giai Đoạn (Segment) cụ thể. Cấm xin nguyên ngày sáo rỗng!
    public virtual ICollection<LeaveRequestSegment> Segments { get; private set; } = new List<LeaveRequestSegment>();

    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? ApproverNote { get; private set; }  // Ghi chú của người duyệt
    public string? RejectReason { get; private set; }  // Lý do từ chối
    public DateTime? RejectedAt { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; } // Nhân viên tự hủy

    protected LeaveRequest() { }

    public LeaveRequest(Guid tenantId, Guid employeeId, Guid leaveTypeId, string leaveType, string? reasonNote, string? attachmentUrl)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        LeaveType = leaveType;
        ReasonNote = reasonNote;
        AttachmentUrl = attachmentUrl;
        Status = "Pending";
    }

    /// <summary>
    /// Hàm duyệt đơn (Chỉ được duyệt khi Pending)
    /// </summary>
    public void Approve(Guid approverId, string? note = null)
    {
        if (Status != StatusEnum.LeaveRequestPending)
            throw new InvalidOperationException("Chỉ duyệt đơn đang trong trạng thái chờ.");
            
        Status = StatusEnum.LeaveRequestApproved;
        ApprovedByUserId = approverId;
        ApprovedAt = DateTime.UtcNow;
        ApproverNote = note;
    }
     
    public void Reject(Guid rejectorId, string reason) 
    {
        if(Status != StatusEnum.LeaveRequestPending)
            throw new InvalidOperationException("Chỉ được từ chối đơn đang trong trạng thái chờ.");

        Status = StatusEnum.LeaveRequestRejected;
        RejectedByUserId = rejectorId;
        RejectedAt = DateTime.UtcNow;
        RejectReason = reason;
    }

    public void Cancel()
    {
        if(Status != StatusEnum.LeaveRequestPending)
            throw new InvalidOperationException("Chỉ được hủy đơn đang trong trạng thái chờ");

        Status = StatusEnum.LeaveRequestCancelled;
        CancelledAt = DateTime.UtcNow;
    }

    public virtual Employee? Employee { get; set; }
    public virtual User? ApprovedByUser { get; set; }
}
