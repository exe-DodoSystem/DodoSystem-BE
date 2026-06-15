using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SMEFLOWSystem.Application.DTOs.Leave;

public class LeaveRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    public string? ReasonNote { get; set; }
    public string? AttachmentUrl { get; set; }
    
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApproverNote { get; set; }
    
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    
    public List<LeaveRequestSegmentDto> Segments { get; set; } = new();
}

public class LeaveRequestSegmentDto
{
    public Guid Id { get; set; }
    public DateOnly LeaveDate { get; set; }
    public Guid? TargetShiftSegmentId { get; set; }
    public string? TargetShiftSegmentName { get; set; }
    public decimal HoursRequested { get; set; }
}

public class ApproveLeaveRequestDto
{
    public string? ApproverNote { get; set; }
}

public class RejectLeaveRequestDto
{
    [Required(ErrorMessage = "Lý do từ chối không được để trống.")]
    [MinLength(5, ErrorMessage = "Lý do từ chối phải có ít nhất 5 ký tự.")]
    public string RejectReason { get; set; } = string.Empty;
}
