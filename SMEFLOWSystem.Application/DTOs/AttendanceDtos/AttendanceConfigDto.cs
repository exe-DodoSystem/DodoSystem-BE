namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

/// <summary>
/// Request body for PUT /api/attendances/config
/// TenantAdmin sets company office location + work schedule.
/// </summary>
public class AttendanceConfigDto
{
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int CheckInRadiusMeters { get; set; } = 100;

    public TimeOnly? WorkStartTime { get; set; }

    public TimeOnly? WorkEndTime { get; set; }

    public int LateThresholdMinutes { get; set; } = 10;

    public int EarlyLeaveThresholdMinutes { get; set; } = 10;
}
