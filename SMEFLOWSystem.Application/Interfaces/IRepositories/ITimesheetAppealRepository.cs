using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ITimesheetAppealRepository
{
    Task AddAsync(TimesheetAppeal appeal);
    Task<TimesheetAppeal?> GetByIdAsync(Guid id);
    Task<List<TimesheetAppeal>> GetByEmployeeAsync(Guid employeeId);
    Task<List<TimesheetAppeal>> GetPendingAsync(Guid tenantId);
    Task UpdateAsync(TimesheetAppeal appeal);
}
