#nullable disable
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

public partial class Notification : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "General";

    /// ID entity liên quan (vd: PayrollId) để FE navigate tới
    public Guid? ReferenceId { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User RecipientUser { get; set; }
    public virtual Tenant Tenant { get; set; }
}
