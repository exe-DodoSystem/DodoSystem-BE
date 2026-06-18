using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IEmployeeSalaryHistoryRepository
    {
        Task AddAsync(EmployeeSalaryHistory history);
        Task<(List<EmployeeSalaryHistory> Items, int TotalCount)> GetPagedByEmployeeIdAsync(
            Guid employeeId,
            int pageNumber,
            int pageSize);
    }
}
