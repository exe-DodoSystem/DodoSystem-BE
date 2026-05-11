using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftPatternQueryDto : PagingRequestDto
    {
        public string? Search { get; set; }
        public bool? IncludeDeleted { get; set; }
    }
}
