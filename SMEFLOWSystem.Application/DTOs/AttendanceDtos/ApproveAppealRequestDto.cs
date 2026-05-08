using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class ApproveAppealRequestDto
{
    public bool IsApproved { get; set; }
    public string? RejectReason { get; set; }
}
