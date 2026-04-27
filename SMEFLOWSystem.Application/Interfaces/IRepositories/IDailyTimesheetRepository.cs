using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IDailyTimesheetRepository
{
    Task<DailyTimesheet?> GetByEmployeeDateAsync(Guid employeeId, DateOnly workDate);
    Task<List<DailyTimesheet>> GetByEmployeeMonthAsync(Guid employeeId, int month, int year);
    Task AddAsync(DailyTimesheet timesheet);
    Task AddRangeAsync(List<DailyTimesheet> timesheets);
    Task UpdateAsync(DailyTimesheet timesheet);
}
