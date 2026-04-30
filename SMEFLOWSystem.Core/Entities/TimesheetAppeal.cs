using System;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

public class TimesheetAppeal : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    
    // In, Out, or Both
    public string AppealType { get; set; } = string.Empty; 
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    
    // PendingApproval, Approved, Rejected
    public string Status { get; set; } = "PendingApproval"; 

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }

    public virtual Employee? Employee { get; set; }
}
