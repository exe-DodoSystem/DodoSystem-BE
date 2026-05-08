using System;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Khiếu nại sửa công (Quên chấm công).
/// </summary>
public class TimesheetAppeal : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid EmployeeId { get; set; }
    /// <summary>Ngày cần sửa công.</summary>
    public DateOnly WorkDate { get; set; }
    
    /// <summary>Loại khiếu nại (Xin sửa giờ In/Out).</summary>
    public string AppealType { get; set; } = string.Empty; 
    /// <summary>Giờ yêu cầu cập nhật lại (Check-In).</summary>
    public DateTime? RequestedCheckIn { get; set; }
    /// <summary>Giờ yêu cầu cập nhật lại (Check-Out).</summary>
    public DateTime? RequestedCheckOut { get; set; }
    
    /// <summary>Lý do khiếu nại.</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>Ảnh minh chứng.</summary>
    public string? AttachmentUrl { get; set; }
    
    // PendingApproval, Approved, Rejected
    public string Status { get; set; } = "PendingApproval"; 

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }

    public virtual Employee? Employee { get; set; }
}
