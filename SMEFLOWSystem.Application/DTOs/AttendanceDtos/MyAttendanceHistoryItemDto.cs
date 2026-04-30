using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class MyAttendanceHistoryItemDto
{
    public DateOnly WorkDate { get; set; }
    public decimal StandardWorkingHours { get; set; }
    public int TotalActualWorkedMinutes { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEarlyLeaveMinutes { get; set; }
    public string SystemAnomalyFlag { get; set; } = string.Empty;
    public bool IsManuallyAdjusted { get; set; }
    
    public List<MyAttendanceSegmentDto> Segments { get; set; } = new List<MyAttendanceSegmentDto>();
}
