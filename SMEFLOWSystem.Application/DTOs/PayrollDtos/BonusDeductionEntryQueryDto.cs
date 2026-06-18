using ShareKernel.Common.Enum;
using System;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class BonusDeductionEntryQueryDto
    {
        public Guid? EmployeeId { get; set; }
        public Guid? DepartmentId { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public BonusDeductionType? Type { get; set; }
        public BonusDeductionCategory? Category { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
