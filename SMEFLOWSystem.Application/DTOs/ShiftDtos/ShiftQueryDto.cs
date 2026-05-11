using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftQueryDto : PagingRequestDto
    {
        public string? Search { get; set; }
        public bool? IncludeDeleted { get; set; }
    }
}
