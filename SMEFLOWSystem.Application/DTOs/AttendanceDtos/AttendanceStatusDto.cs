namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

/// <summary>
/// Response for GET /api/attendances/my-status
/// Returns today's attendance record + company office location for map display.
/// </summary>
public class AttendanceStatusDto
{
    // Today's attendance (null if not checked in yet)
    public AttendanceDto? TodayAttendance { get; set; }

    // Company office location — used by frontend to show map + calculate proximity
    public double? OfficeLatitude { get; set; }
    public double? OfficeLongitude { get; set; }
    public int CheckInRadiusMeters { get; set; }

    // Work schedule info (for display)
    public TimeOnly? WorkStartTime { get; set; }
    public TimeOnly? WorkEndTime { get; set; }
}
