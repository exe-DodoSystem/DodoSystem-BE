using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IManualMonthlyTimesheetRepository
{
    Task AddAsync(ManualMonthlyTimesheet timesheet);
    Task UpdateAsync(ManualMonthlyTimesheet timesheet);
    Task DeleteAsync(ManualMonthlyTimesheet timesheet);
    Task<ManualMonthlyTimesheet?> GetByIdAsync(Guid id);
    Task<ManualMonthlyTimesheet?> GetByEmployeeMonthYearAsync(Guid tenantId, Guid employeeId, int month, int year);
    Task<List<ManualMonthlyTimesheet>> GetByTenantMonthYearAsync(Guid tenantId, int month, int year);
}
