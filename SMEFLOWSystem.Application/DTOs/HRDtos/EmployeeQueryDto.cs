using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.DTOs.HRDtos;

public class EmployeeQueryDto : PagingRequestDto
{

    public string? SortBy { get; init; } = "FullName";
    public string? SortDir { get; init; } = "asc";

    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public int? RoleId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }

    public bool IncludeResigned { get; init; } = false;
}
