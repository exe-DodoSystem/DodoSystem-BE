using System;

namespace SMEFLOWSystem.Application.DTOs.HRDtos
{
    public class EmployeeSalaryHistoryDto
    {
        public Guid Id { get; set; }
        public decimal OldSalary { get; set; }
        public decimal NewSalary { get; set; }
        public decimal Change { get; set; }        // NewSalary - OldSalary
        public DateOnly EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public string? ChangedByName { get; set; } // Tên người thay đổi
        public DateTime CreatedAt { get; set; }
    }
}
