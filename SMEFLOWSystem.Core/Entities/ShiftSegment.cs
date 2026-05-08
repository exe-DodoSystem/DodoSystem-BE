using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Các khoảng thời gian trong một ca (VD: Ca Sáng, Ca Chiều).
/// </summary>
public partial class ShiftSegment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ShiftId { get; set; }

    /// <summary>Giờ bắt đầu phân đoạn ca.</summary>
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    /// <summary>Độ dời ngày bắt đầu cho ca qua đêm.</summary>
    public int StartDayOffset { get; set; }
    public int EndDayOffset { get; set; }
    public virtual Shift Shift { get; set; } = new Shift();
}