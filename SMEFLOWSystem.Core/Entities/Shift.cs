using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Định nghĩa Ca làm việc.
/// </summary>
public partial class Shift : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>Mã ca làm việc.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Số phút du di (Grace Period).</summary>
    public int GracePeriodMinutes { get; set; }
    /// <summary>Đánh dấu ca làm việc qua đêm.</summary>
    public bool IsCrossDay { get; set; }
    public bool IsDeleted { get; set; }
    public virtual ICollection<ShiftSegment> Segments { get; set; } = new List<ShiftSegment>();
}