#nullable disable
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

public class TenantAttendanceSetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CheckInRadiusMeters { get; set; } = 100;
    public TimeOnly? WorkStartTime { get; set; }
    public TimeOnly? WorkEndTime { get; set; }
    public int LateThresholdMinutes { get; set; } = 10;
    public int EarlyLeaveThresholdMinutes { get; set; } = 10;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public virtual Tenant Tenant { get; set; }
}
