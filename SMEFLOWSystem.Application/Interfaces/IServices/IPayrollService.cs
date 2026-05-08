using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IPayrollService
    {
        Task<bool> GenerateMonthlyPayrollAsync(Guid tenantId, int month, int year);
        Task<PayrollDto> CalculatePayrollForEmployeeAsync(Guid tenantId, Guid employeeId, int month, int year);
        Task<PagedResultDto<PayrollDto>> GetPagedAsync(PayrollQueryDto query);
        Task<List<PayrollDto>> GetMyPayrollAsync(int? month, int? year);
        Task<bool> ApproveAsync(Guid payrollId);
        Task<bool> RejectAsync(Guid payrollId, string reason);
        Task<bool> MarkPaidAsync(Guid payrollId);
        Task<PayrollDto> UpdateBonusDeductionAsync(Guid payrollId, UpdatePayrollDto dto);
    }
}
