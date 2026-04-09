namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class AttendanceConfigResponseDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CheckInRadiusMeters { get; set; }

    public TimeOnly? WorkStartTime { get; set; }
    public TimeOnly? WorkEndTime { get; set; }
    public int LateThresholdMinutes { get; set; }
    public int EarlyLeaveThresholdMinutes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsLocationConfigured => Latitude.HasValue && Longitude.HasValue;
}
