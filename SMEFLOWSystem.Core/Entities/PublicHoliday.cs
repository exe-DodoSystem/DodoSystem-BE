using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Domain Entity: Ngày nghỉ lễ của hệ thống (Public Holiday).
/// </summary>
public class PublicHoliday : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRecurringYearly { get; set; }
}
