using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class PublicHolidayDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRecurringYearly { get; set; }
}
