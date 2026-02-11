using System;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

public class BillingOrderModule : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid BillingOrderId { get; set; }

    public int ModuleId { get; set; }

    public int Quantity { get; set; } = 1;

    // Unit price for this order line (VND)
    public decimal UnitPrice { get; set; }

    // Optional proration days (for add-module mid-cycle)
    public int? ProrationDays { get; set; }

    public decimal LineTotal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual BillingOrder? BillingOrder { get; set; }

    public virtual Module? Module { get; set; }
}
