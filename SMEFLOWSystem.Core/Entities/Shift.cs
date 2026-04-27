using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

public partial class Shift : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int GracePeriodMinutes { get; set; }
    public bool IsCrossDay { get; set; }
    public virtual ICollection<ShiftSegment> Segments { get; set; } = new List<ShiftSegment>();
}