using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class MyCurrentShiftAssignmentDto : EmployeeShiftPatternDto
    {
        public ShiftPatternDto? ShiftPattern { get; set; }
    }
}
