using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IShiftAssignmentRepository
{
    Task<(List<EmployeeShiftPattern> Items, int TotalCount)> GetPagedAsync(
        Guid? employeeId,
        Guid? departmentId,
        Guid? shiftPatternId,
        bool? isActiveOnly,
        int pageNumber,
        int pageSize,
        DateOnly today);

    Task<EmployeeShiftPattern?> GetByIdAsync(Guid id);
    Task BulkEndPreviousAssignmentsAsync(IEnumerable<Guid> employeeIds, DateOnly newStartDate);
    Task BulkInsertAssignmentsAsync(List<EmployeeShiftPattern> assignments);
}
