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

        /// <summary>[TenantAdmin] Tạo bảng lương nháp (Draft) hàng tháng cho tất cả nhân viên</summary>
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

        /// <summary>[TenantAdmin, HRManager] Tính toán lại lương cho một nhân viên cụ thể</summary>
        [HttpPost("calculate/{employeeId}")]
        [Authorize(Roles = "TenantAdmin,HRManager")]
        public async Task<IActionResult> CalculateForEmployee(Guid employeeId, [FromQuery] int month, [FromQuery] int year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var result = await _payrollService.CalculatePayrollForEmployeeAsync(tenantId, employeeId, month, year);
            return Ok(result);
        }

        /// <summary>[TenantAdmin, HRManager, Manager] Lấy danh sách bảng lương có phân trang</summary>
        [HttpGet("paged")]
        [Authorize(Roles = "TenantAdmin,HRManager,Manager")]
        public async Task<IActionResult> GetPaged([FromQuery] PayrollQueryDto query)
        {
            var result = await _payrollService.GetPagedAsync(query);
            return Ok(result);
        }

        /// <summary>Lấy thông tin phiếu lương của chính user đang đăng nhập</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyPayroll([FromQuery] int? month, [FromQuery] int? year)
        {
            var result = await _payrollService.GetMyPayrollAsync(month, year);
            return Ok(result);
        }

        /// <summary>[TenantAdmin] Phê duyệt phiếu lương</summary>
        [HttpPut("{payrollId}/approve")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Approve(Guid payrollId)
        {
            var result = await _payrollService.ApproveAsync(payrollId);
            return Ok(new { approved = result });
        }

        /// <summary>[TenantAdmin] Từ chối phiếu lương</summary>
        [HttpPut("{payrollId}/reject")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Reject(Guid payrollId, [FromBody] RejectPayrollRequest request)
        {
            var result = await _payrollService.RejectAsync(payrollId, request.Reason);
            return Ok(new { rejected = result });
        }

        /// <summary>[TenantAdmin] Đánh dấu phiếu lương đã được thanh toán</summary>
        [HttpPut("{payrollId}/mark-paid")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> MarkPaid(Guid payrollId)
        {
            var result = await _payrollService.MarkPaidAsync(payrollId);
            return Ok(new { paid = result });
        }

        /// <summary>[TenantAdmin, HRManager, Manager] Cập nhật tiền thưởng/phạt cho phiếu lương</summary>
        [HttpPut("{payrollId}/bonus-deduction")]
        [Authorize(Roles = "TenantAdmin,HRManager,Manager")]
        public async Task<IActionResult> UpdateBonusDeduction(Guid payrollId, [FromBody] UpdatePayrollDto dto)
        {
            var result = await _payrollService.UpdateBonusDeductionAsync(payrollId, dto);
            return Ok(result);
        }
    }
}
