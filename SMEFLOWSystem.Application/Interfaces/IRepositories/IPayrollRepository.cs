using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IPayrollRepository
    {
        Task<Payroll?> GetByIdAsync(Guid payrollId);
        Task<List<Payroll>> GetByEmployeeMonthAsync(Guid employeeId, Guid tenantId, int month, int year);
        Task<List<Payroll>> GetByTenantMonthAsync(Guid tenantId, int month, int year);
        Task<List<Payroll>> GetDraftsByTenantMonthAsync(Guid tenantId, int month, int year);
        Task AddAsync(Payroll payroll);
        Task AddRangeAsync(List<Payroll> payrolls);
        Task<Payroll> UpdateAsync(Payroll payroll);
        Task UpdateRangeAsync(List<Payroll> payrolls);

        Task<(List<Payroll> Items, int TotalCount)> GetPagedAsync(
            Guid tenantId,
            Guid? departmentId,
            Guid? employeeId,
            int? month,
            int? year,
            string? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            string? sortDir);

        Task<(List<Payroll> Items, int TotalCount)> GetByEmployeeIdPagedAsync(
            Guid employeeId,
            int? month,
            int? year,
            int pageNumber,
            int pageSize);
    }
}
