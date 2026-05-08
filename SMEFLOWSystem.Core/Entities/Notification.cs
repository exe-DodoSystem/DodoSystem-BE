#nullable disable
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Hệ thống thông báo nội bộ.
/// </summary>
public partial class Notification : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>ID người nhận thông báo.</summary>
    public Guid RecipientUserId { get; set; }

    /// <summary>Tiêu đề thông báo.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Nội dung thông báo.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Loại thông báo.</summary>
    public string Type { get; set; } = "General";

    /// ID entity liên quan (vd: PayrollId) để FE navigate tới
    public Guid? ReferenceId { get; set; }

    /// <summary>Trạng thái đã đọc.</summary>
    public bool IsRead { get; set; } = false;
    /// <summary>Thời điểm đọc.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User RecipientUser { get; set; }
    public virtual Tenant Tenant { get; set; }
}
