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
        Task<PagedResultDto<PayrollDto>> GetPagedAsync(Guid tenantId, PayrollQueryDto query);
        Task<List<PayrollDto>> GetMyPayrollAsync(Guid tenantId, Guid userId, int? month, int? year);
        Task<bool> MarkPaidAsync(Guid payrollId); 
   
        Task<PayrollDto> UpdateManualFieldsAsync(Guid payrollId, UpdatePayrollDto dto);
        Task<bool> PublishPayrollAsync(Guid payrollId);
        Task<int> PublishAllDraftAsync(Guid tenantId, int month, int year);

        /// <summary>[TenantAdmin, HRManager] Gán thưởng/phạt cho nhân viên theo tháng/năm</summary>
        Task<PayrollDto> SetBonusPenaltyByEmployeeAsync(Guid tenantId, EmployeeBonusPenaltyDto dto);

        /// <summary>[TenantAdmin, HRManager] Gán thưởng/phạt hàng loạt</summary>
        Task<List<PayrollDto>> BulkSetBonusPenaltyAsync(Guid tenantId, BulkBonusPenaltyDto dto);
    }
}
