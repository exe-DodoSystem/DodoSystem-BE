using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ICurrentUserService _currentUser;

        public DashboardController(
            IDashboardService dashboardService,
            ICurrentTenantService currentTenant,
            ICurrentUserService currentUser)
        {
            _dashboardService = dashboardService;
            _currentTenant = currentTenant;
            _currentUser = currentUser;
        }

        /// <summary>[TenantAdmin, HRManager] Dashboard tổng quan toàn công ty</summary>
        [HttpGet("admin")]
        [Authorize(Policy = PolicyNames.AdminOrHr)]
        public async Task<IActionResult> GetAdminDashboard([FromQuery] int? month, [FromQuery] int? year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");

            var m = month ?? DateTime.UtcNow.Month;
            var y = year ?? DateTime.UtcNow.Year;
            var result = await _dashboardService.GetAdminDashboardAsync(tenantId, m, y);
            return Ok(result);
        }

        /// <summary>[Manager] Dashboard phòng ban mình quản lý</summary>
        [HttpGet("manager")]
        [Authorize(Policy = PolicyNames.Manager)]
        public async Task<IActionResult> GetManagerDashboard([FromQuery] int? month, [FromQuery] int? year)
        {
            var tenantId = _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");
            var userId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("Không thể xác định danh tính người dùng.");

            var m = month ?? DateTime.UtcNow.Month;
            var y = year ?? DateTime.UtcNow.Year;
            var result = await _dashboardService.GetManagerDashboardAsync(tenantId, userId, m, y);
            return Ok(result);
        }

        /// <summary>Dashboard cá nhân cho mọi user đã đăng nhập</summary>
        [HttpGet("employee")]
        public async Task<IActionResult> GetEmployeeDashboard([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("Không thể xác định danh tính người dùng.");

            var m = month ?? DateTime.UtcNow.Month;
            var y = year ?? DateTime.UtcNow.Year;
            var result = await _dashboardService.GetEmployeeDashboardAsync(userId, m, y);
            return Ok(result);
        }
    }
}
