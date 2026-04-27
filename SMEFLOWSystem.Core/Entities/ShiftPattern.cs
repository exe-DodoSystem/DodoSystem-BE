using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities
{
    public partial class ShiftPattern : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CycleLengthDays { get; set; }
        public virtual ICollection<ShiftPatternDay> Days { get; set; } = new List<ShiftPatternDay>();
    }
}

