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

    // Lấy Shift kèm Segments theo ShiftId
    Task<Shift?> GetShiftWithSegmentsAsync(Guid shiftId);

    Task<ShiftPatternDay?> GetShiftPatternWithDaysAsync(Guid shiftPatternId, int dayIndex);
}
