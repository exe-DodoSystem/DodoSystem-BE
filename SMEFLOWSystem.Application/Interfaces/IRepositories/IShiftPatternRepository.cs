using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IShiftPatternRepository
{
    /// <summary>
    /// Lấy EmployeeShiftPattern đang active cho một nhân viên tại ngày chỉ định,
    /// kèm theo ShiftPattern -> Days -> ScheduledShift -> Segments
    /// </summary>
    Task<EmployeeShiftPattern?> GetActivePatternForEmployeeAsync(Guid employeeId, DateOnly targetDate);

    /// <summary>
    /// Lấy EmployeeShiftPattern và ShiftPattern cho một nhân viên tại ngày chỉ định
    /// </summary>
    Task<(EmployeeShiftPattern? Pattern, ShiftPattern? Definition)> GetActivePatternDetailsAsync(Guid employeeId, DateOnly targetDate);

    /// <summary>
    /// Lấy Shift kèm Segments theo ShiftId
    /// </summary>
    Task<Shift?> GetShiftWithSegmentsAsync(Guid shiftId);
}
