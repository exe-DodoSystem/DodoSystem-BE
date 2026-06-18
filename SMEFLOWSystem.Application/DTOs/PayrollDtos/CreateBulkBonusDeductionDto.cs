using ShareKernel.Common.Enum;
using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class CreateBulkBonusDeductionDto
    {
        public List<Guid> EmployeeIds { get; set; } = new();
        public int Month { get; set; }
        public int Year { get; set; }
        public BonusDeductionType Type { get; set; }
        public BonusDeductionCategory Category { get; set; }
        public decimal Amount { get; set; }    // cùng mức cho tất cả
        public string? Reason { get; set; }
    }
}
