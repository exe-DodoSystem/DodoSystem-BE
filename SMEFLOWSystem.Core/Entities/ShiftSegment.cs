using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

public partial class ShiftSegment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ShiftId { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int StartDayOffset { get; set; }
    public int EndDayOffset { get; set; }
    public virtual Shift Shift { get; set; } = new Shift();
}