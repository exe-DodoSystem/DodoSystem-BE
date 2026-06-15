using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ILeaveBalanceRepository
{
    Task<EmployeeLeaveBalance?> GetByIdAsync(Guid id);
    Task<EmployeeLeaveBalance?> GetByEmployeeTypeYearAsync(Guid employeeId, Guid leaveTypeId, int year);
    Task<List<EmployeeLeaveBalance>> GetByEmployeeAsync(Guid employeeId, int year);
    Task<List<EmployeeLeaveBalance>> GetAllAsync(int year);
    Task AddAsync(EmployeeLeaveBalance balance);
    Task UpdateAsync(EmployeeLeaveBalance balance);
    Task AddRangeAsync(IEnumerable<EmployeeLeaveBalance> balances);
}
