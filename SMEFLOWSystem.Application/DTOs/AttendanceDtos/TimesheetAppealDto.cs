using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class TimesheetAppealDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string AppealType { get; set; } = string.Empty; 
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = string.Empty; 
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }
}
