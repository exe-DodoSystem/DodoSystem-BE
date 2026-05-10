using SharedKernel.DTOs;
using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftAssignmentQueryDto : PagedQueryDto
    {
        public Guid? EmployeeId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? ShiftPatternId { get; set; }
        public bool? IsActiveOnly { get; set; } // Nếu true, chỉ lấy những assignment có EffectiveEndDate >= today hoặc null
    }
}
