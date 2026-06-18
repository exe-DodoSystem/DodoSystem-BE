using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IShiftManagementService
{
    Task<PagedResultDto<ShiftDto>> GetPagedAsync(ShiftQueryDto query);
    Task<ShiftDto> GetByIdAsync(Guid id);
    Task<ShiftDto> CreateAsync(ShiftCreateDto request);
    Task<ShiftDto> UpdateAsync(Guid id, ShiftCreateDto request);
    Task DeleteAsync(Guid id);

    Task<PagedResultDto<ShiftPatternDto>> GetPatternsPagedAsync(ShiftPatternQueryDto query);
    Task<ShiftPatternDto> GetPatternByIdAsync(Guid id);
    Task<ShiftPatternDto> CreatePatternAsync(ShiftPatternCreateDto request);
    Task<ShiftPatternDto> UpdatePatternAsync(Guid id, ShiftPatternCreateDto request);
    Task DeletePatternAsync(Guid id);

    Task<List<EmployeeShiftPatternDto>> BulkAssignPatternAsync(ShiftAssignmentBulkCreateDto request);
    Task<PagedResultDto<EmployeeShiftPatternDto>> GetAssignmentsPagedAsync(ShiftAssignmentQueryDto query);
    Task<EmployeeShiftPatternDto> GetAssignmentByIdAsync(Guid id);
    Task<MyCurrentShiftAssignmentDto?> GetMyCurrentAssignmentAsync(Guid userId);
    Task<MyScheduleDto?> GetMyScheduleAsync(Guid userId, DateOnly? fromDate, DateOnly? toDate, bool includeOffDays);
}
