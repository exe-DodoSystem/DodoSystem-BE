using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using System.Security.Claims;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

using SMEFLOWSystem.WebAPI.Filters;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/payrolls")]
    [ApiController]
    [Authorize]
    [RequireModule("PAYROLL")]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _payrollService;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ICurrentUserService _currentUser;

        public PayrollController(
            IPayrollService payrollService, 
            ICurrentTenantService currentTenant,
            ICurrentUserService currentUser)
        {
            _payrollService = payrollService;
            _currentTenant = currentTenant;
            _currentUser = currentUser;
        }

        /// <summary>[TenantAdmin] Tạo bảng lương nháp (Draft) hàng tháng cho tất cả nhân viên</summary>
        [HttpPost("generate")]
        [Authorize(Policy = PolicyNames.TenantAdmin)]
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
        [Authorize(Policy = PolicyNames.AdminOrHr)]
        public async Task<IActionResult> CalculateForEmployee(Guid employeeId, [FromQuery] int month, [FromQuery] int year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var result = await _payrollService.CalculatePayrollForEmployeeAsync(tenantId, employeeId, month, year);
            return Ok(result);
        }

        /// <summary>[TenantAdmin, HRManager, Manager] Lấy danh sách bảng lương có phân trang</summary>
        [HttpGet("paged")]
        [Authorize(Policy = PolicyNames.HrAccess)]
        public async Task<IActionResult> GetPaged([FromQuery] PayrollQueryDto query)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var result = await _payrollService.GetPagedAsync(tenantId, query);
            return Ok(result);
        }

        /// <summary>Lấy thông tin phiếu lương của chính user đang đăng nhập</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyPayroll([FromQuery] int? month, [FromQuery] int? year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var userId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("Không thể xác định danh tính người dùng.");

            var result = await _payrollService.GetMyPayrollAsync(tenantId, userId, month, year);
            return Ok(result);
        }

        /// <summary>[TenantAdmin, HRManager] Chốt phiếu lương (Publish)</summary>
        [HttpPut("{payrollId}/publish")]
        [Authorize(Policy = PolicyNames.AdminOrHr)]
        public async Task<IActionResult> Publish(Guid payrollId)
        {
            var result = await _payrollService.PublishPayrollAsync(payrollId);
            return Ok(new { published = result });
        }

        /// <summary>[TenantAdmin] Đánh dấu phiếu lương đã thanh toán</summary>
        [HttpPut("{payrollId}/mark-paid")]
        [Authorize(Policy = PolicyNames.TenantAdmin)]
        public async Task<IActionResult> MarkPaid(Guid payrollId)
        {
            var result = await _payrollService.MarkPaidAsync(payrollId);
            return Ok(new { paid = result });
        }

        /// <summary>[TenantAdmin] Chốt tất cả phiếu lương Draft trong tháng</summary>
        [HttpPut("publish-all")]
        [Authorize(Policy = PolicyNames.TenantAdmin)]
        public async Task<IActionResult> PublishAll([FromQuery] int month, [FromQuery] int year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var count = await _payrollService.PublishAllDraftAsync(tenantId, month, year);
            return Ok(new { message = $"Đã chốt {count} phiếu lương.", publishedCount = count });
        }

        /// <summary>[TenantAdmin, HRManager, Manager] Cập nhật tiền thưởng/phạt nhập tay cho phiếu lương</summary>
        [HttpPut("{payrollId}/manual-fields")]
        [Authorize(Policy = PolicyNames.HrAccess)]
        public async Task<IActionResult> UpdateManualFields(Guid payrollId, [FromBody] UpdatePayrollDto dto)
        {
            var result = await _payrollService.UpdateManualFieldsAsync(payrollId, dto);
            return Ok(result);
        }
    }
}
