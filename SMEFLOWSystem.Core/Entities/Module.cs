using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Core.Entities;

public class Module
{
    public int Id { get; set; }

    // e.g. "HR", "ATTENDANCE"...
    public string Code { get; set; } = string.Empty;

    // e.g. "HR", "ATT", "SALES"...
    public string ShortCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Monthly price (VND)
    public decimal MonthlyPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ModuleSubscription> ModuleSubscriptions { get; set; } = new List<ModuleSubscription>();
    public virtual ICollection<BillingOrderModule> BillingOrderModules { get; set; } = new List<BillingOrderModule>();
}
