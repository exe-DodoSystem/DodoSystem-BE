using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class AttendanceSettingDto
{
    public Guid TenantId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CheckInRadiusMeters { get; set; }
    public TimeSpan? WorkStartTime { get; set; }
    public TimeSpan? WorkEndTime { get; set; }
    public TimeSpan DayStartCutOffTime { get; set; }
    public int LateThresholdMinutes { get; set; }
    public int EarlyLeaveThresholdMinutes { get; set; }
    public int MinimumOTMinutes { get; set; }
    public int OTBlockMinutes { get; set; }
}

public class UpdateAttendanceSettingRequestDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CheckInRadiusMeters { get; set; }
    public TimeSpan? WorkStartTime { get; set; }
    public TimeSpan? WorkEndTime { get; set; }
    public TimeSpan DayStartCutOffTime { get; set; }
    public int LateThresholdMinutes { get; set; }
    public int EarlyLeaveThresholdMinutes { get; set; }
    public int MinimumOTMinutes { get; set; }
    public int OTBlockMinutes { get; set; }
}
