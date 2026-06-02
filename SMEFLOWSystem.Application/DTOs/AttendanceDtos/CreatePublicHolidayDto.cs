using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class CreatePublicHolidayDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRecurringYearly { get; set; }
}
