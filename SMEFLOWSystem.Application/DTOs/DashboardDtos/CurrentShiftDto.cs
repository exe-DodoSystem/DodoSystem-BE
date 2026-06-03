using System;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class CurrentShiftDto
{
    public Guid? ShiftPatternId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}
