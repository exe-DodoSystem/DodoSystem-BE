using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Định nghĩa các gói chức năng của phần mềm (Ví dụ: Chấm công, Tính lương).
/// </summary>
public class Module
{
    /// <summary>Mã ID module.</summary>
    public int Id { get; set; }

    // e.g. "HR", "ATTENDANCE"...
    /// <summary>Mã code module (e.g. "HR", "ATTENDANCE").</summary>
    public string Code { get; set; } = string.Empty;

    // e.g. "HR", "ATT", "SALES"...
    /// <summary>Mã rút gọn.</summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Tên gói module.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết module.</summary>
    public string Description { get; set; } = string.Empty;

    // Monthly price (VND)
    /// <summary>Giá thuê mỗi tháng (VND).</summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>Trạng thái hoạt động của module.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ModuleSubscription> ModuleSubscriptions { get; set; } = new List<ModuleSubscription>();
    public virtual ICollection<BillingOrderModule> BillingOrderModules { get; set; } = new List<BillingOrderModule>();
}
