using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Quản lý việc một Tenant đã mua gói Module nào, thời hạn sử dụng.
/// </summary>
public class ModuleSubscription : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public int ModuleId { get; set; }

    /// <summary>Ngày bắt đầu sử dụng gói.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Ngày hết hạn gói.</summary>
    public DateTime EndDate { get; set; }

    // Trial | Active | Suspended
    /// <summary>Trạng thái gói (Trial | Active | Suspended).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Tenant? Tenant { get; set; }

    public virtual Module? Module { get; set; }
}
