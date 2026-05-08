using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class MyAttendanceSegmentDto
{
    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
}
