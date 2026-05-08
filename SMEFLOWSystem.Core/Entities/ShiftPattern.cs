using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Mẫu chu kỳ phân ca làm việc.
    /// </summary>
    public partial class ShiftPattern : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Số ngày trong chu kỳ.</summary>
        public int CycleLengthDays { get; set; }
        public virtual ICollection<ShiftPatternDay> Days { get; set; } = new List<ShiftPatternDay>();
    }
}

