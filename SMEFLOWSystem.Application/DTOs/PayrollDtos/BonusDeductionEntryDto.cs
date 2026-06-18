using System;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class BonusDeductionEntryDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public string Type { get; set; } = string.Empty;       // "Bonus" / "Deduction"
        public string Category { get; set; } = string.Empty;   // "Performance" / "Holiday" / ...
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }
    }
}
