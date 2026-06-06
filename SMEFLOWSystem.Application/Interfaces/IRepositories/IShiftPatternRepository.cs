using SMEFLOWSystem.Core.Entities;
using System.Runtime.CompilerServices;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IShiftPatternRepository
{

    // Lấy EmployeeShiftPattern đang active cho một nhân viên tại ngày chỉ định,
    // kèm theo ShiftPattern -> Days -> ScheduledShift -> Segments
    Task<EmployeeShiftPattern?> GetActivePatternForEmployeeAsync(Guid employeeId, DateOnly targetDate);


    // Lấy EmployeeShiftPattern và ShiftPattern cho một nhân viên tại ngày chỉ định
    Task<(EmployeeShiftPattern? Pattern, ShiftPattern? Definition)> GetActivePatternDetailsAsync(Guid employeeId, DateOnly targetDate);

    // Bulk Load
    Task<List<EmployeeShiftPattern>> GetActivePatternsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate);
    Task<List<ShiftPattern>> GetPatternsWithDaysAsync(List<Guid> patternIds);
    Task<List<Shift>> GetShiftsWithSegmentsAsync(List<Guid> shiftIds);

    // Lấy Shift kèm Segments theo ShiftId
    Task<Shift?> GetShiftWithSegmentsAsync(Guid shiftId);

    Task<ShiftPatternDay?> GetShiftPatternWithDaysAsync(Guid shiftPatternId, int dayIndex);

    // CRUD + Query cho ShiftPattern
    Task<(List<ShiftPattern> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool includeDeleted,
        int pageNumber,
        int pageSize);

    Task<ShiftPattern?> GetByIdWithDaysAsync(Guid id);
    Task AddAsync(ShiftPattern pattern);
    Task<ShiftPattern> UpdateAsync(ShiftPattern pattern);
    Task DeleteAsync(ShiftPattern pattern);
    Task<bool> HasUsageAsync(Guid shiftPatternId);
    Task<bool> ShiftExistsAsync(Guid shiftId);
    Task DeletePatternDaysAsync(Guid shiftPatternId);
}
