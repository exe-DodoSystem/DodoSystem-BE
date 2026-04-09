using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class AttendanceQueryDto : PagingRequestDto
{
    public string? SortBy { get; init; } = "WorkDate";
    public string? SortDir { get; init; } = "desc";

    public Guid? DepartmentId { get; init; }

    public Guid? EmployeeId { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }

    public string? Status { get; init; }

    public string? ApprovalStatus { get; init; }

    public string? Search { get; init; }
}
