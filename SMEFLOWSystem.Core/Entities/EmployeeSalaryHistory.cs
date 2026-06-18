using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities
{
    public class EmployeeSalaryHistory : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public decimal OldSalary { get; set; }
        public decimal NewSalary { get; set; }
        public DateOnly EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public Guid? ChangedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public virtual Employee Employee { get; set; } = null!;
        public virtual User? ChangedByUser { get; set; }
    }
}
