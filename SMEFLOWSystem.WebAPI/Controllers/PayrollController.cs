using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/payrolls")]
    [ApiController]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _payrollService;
        private readonly ICurrentTenantService _currentTenant;

        public PayrollController(IPayrollService payrollService, ICurrentTenantService currentTenant)
        {
            _payrollService = payrollService;
            _currentTenant = currentTenant;
        }

        [HttpPost("generate")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> GenerateMonthlyPayroll([FromQuery] int month, [FromQuery] int year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var result = await _payrollService.GenerateMonthlyPayrollAsync(tenantId, month, year);
            if (!result)
                return Ok(new { message = "Tất cả nhân viên đã có phiếu lương cho tháng này." });

            return Ok(new { message = "Tạo phiếu lương Draft thành công." });
        }

        [HttpPost("calculate/{employeeId}")]
        [Authorize(Roles = "TenantAdmin,HRManager")]
        public async Task<IActionResult> CalculateForEmployee(Guid employeeId, [FromQuery] int month, [FromQuery] int year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var result = await _payrollService.CalculatePayrollForEmployeeAsync(tenantId, employeeId, month, year);
            return Ok(result);
        }

        [HttpGet("paged")]
        [Authorize(Roles = "TenantAdmin,HRManager,Manager")]
        public async Task<IActionResult> GetPaged([FromQuery] PayrollQueryDto query)
        {
            var result = await _payrollService.GetPagedAsync(query);
            return Ok(result);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyPayroll([FromQuery] int? month, [FromQuery] int? year)
        {
            var result = await _payrollService.GetMyPayrollAsync(month, year);
            return Ok(result);
        }

        [HttpPut("{payrollId}/approve")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Approve(Guid payrollId)
        {
            var result = await _payrollService.ApproveAsync(payrollId);
            return Ok(new { approved = result });
        }

        [HttpPut("{payrollId}/reject")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Reject(Guid payrollId, [FromBody] RejectPayrollRequest request)
        {
            var result = await _payrollService.RejectAsync(payrollId, request.Reason);
            return Ok(new { rejected = result });
        }

        [HttpPut("{payrollId}/mark-paid")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> MarkPaid(Guid payrollId)
        {
            var result = await _payrollService.MarkPaidAsync(payrollId);
            return Ok(new { paid = result });
        }

        [HttpPut("{payrollId}/bonus-deduction")]
        [Authorize(Roles = "TenantAdmin,HRManager,Manager")]
        public async Task<IActionResult> UpdateBonusDeduction(Guid payrollId, [FromBody] UpdatePayrollDto dto)
        {
            var result = await _payrollService.UpdateBonusDeductionAsync(payrollId, dto);
            return Ok(result);
        }
    }
}
