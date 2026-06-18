using ShareKernel.Common.Enum;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities
{
    public class EmployeeBonusDeductionEntry : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public BonusDeductionType Type { get; set; }
        public BonusDeductionCategory Category { get; set; }
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public virtual Employee Employee { get; set; } = null!;
        public virtual User? CreatedByUser { get; set; }
    }
}
