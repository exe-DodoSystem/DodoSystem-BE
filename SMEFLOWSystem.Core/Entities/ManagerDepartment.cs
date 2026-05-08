using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Bảng trung gian N-N: một Manager có thể quản lý nhiều Department,
/// và một Department có thể được giao cho nhiều Manager.
/// TenantAdmin là người quyết định ai quản lý phòng ban nào.
/// </summary>
public class ManagerDepartment : ITenantEntity
{
    /// <summary>FK → Users — User có role Manager được giao quyền</summary>
    public Guid UserId { get; set; }

    /// <summary>FK → Departments — Phòng ban được giao</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Tenant context (multi-tenant isolation)</summary>
    public Guid TenantId { get; set; }

    /// <summary>Thời điểm được giao quyền</summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>UserId của người thực hiện giao quyền (TenantAdmin). </summary>
    public Guid AssignedByUserId { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Department Department { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}
