using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.Leave;

public class SubmitLeaveRequestDto
{
    public Guid LeaveTypeId { get; set; }
    public string? ReasonNote { get; set; }
    public string? AttachmentUrl { get; set; }
    public List<SubmitLeaveRequestDayDto> Days { get; set; } = new();
}

public class SubmitLeaveRequestDayDto
{
    public DateOnly LeaveDate { get; set; }
    public Guid? TargetShiftSegmentId { get; set; } // Null if whole day
    public decimal HoursRequested { get; set; }    // e.g. 4 for half day, 8 for whole day
}
