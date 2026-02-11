using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

public class ModuleSubscription : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public int ModuleId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    // Trial | Active | Suspended
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Tenant? Tenant { get; set; }

    public virtual Module? Module { get; set; }
}
