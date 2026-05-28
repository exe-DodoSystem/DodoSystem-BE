using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class EmployeeShiftPatternDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeDepartment { get; set; } = string.Empty;

        public Guid ShiftPatternId { get; set; }
        public string ShiftPatternName { get; set; } = string.Empty;

        public DateOnly EffectiveStartDate { get; set; }
        public DateOnly? EffectiveEndDate { get; set; }
    }
}
