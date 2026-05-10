using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftPatternQueryDto : PagedQueryDto
    {
        public string? Search { get; set; }
        public bool? IncludeDeleted { get; set; }
    }
}
