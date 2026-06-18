using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IBonusDeductionEntryRepository
    {
        Task AddAsync(EmployeeBonusDeductionEntry entry);
        Task AddRangeAsync(IEnumerable<EmployeeBonusDeductionEntry> entries);
        Task DeleteAsync(EmployeeBonusDeductionEntry entry);
        Task<EmployeeBonusDeductionEntry?> GetByIdAsync(Guid id);
        Task<List<EmployeeBonusDeductionEntry>> GetByTenantMonthYearAsync(Guid tenantId, int month, int year);
        Task<List<EmployeeBonusDeductionEntry>> GetByEmployeeMonthYearAsync(Guid tenantId, Guid employeeId, int month, int year);
        Task<(List<EmployeeBonusDeductionEntry> Items, int TotalCount)> GetPagedAsync(
            BonusDeductionEntryQueryDto query,
            Guid tenantId,
            List<Guid>? accessibleDepartmentIds);
    }
}
