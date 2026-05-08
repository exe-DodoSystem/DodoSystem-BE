using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class SubmitAppealRequestDto
{
    public DateOnly WorkDate { get; set; }
    public string AppealType { get; set; } = string.Empty; // In, Out, Both
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}
