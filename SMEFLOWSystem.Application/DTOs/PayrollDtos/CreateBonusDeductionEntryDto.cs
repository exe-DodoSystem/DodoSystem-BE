using ShareKernel.Common.Enum;
using System;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class CreateBonusDeductionEntryDto
    {
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public BonusDeductionType Type { get; set; }
        public BonusDeductionCategory Category { get; set; }
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
    }
}
