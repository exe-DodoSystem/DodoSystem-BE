using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/dashboard")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public class SystemDashboardController : ControllerBase
{
    private readonly ISystemDashboardService _systemDashboardService;

    public SystemDashboardController(ISystemDashboardService systemDashboardService)
    {
        _systemDashboardService = systemDashboardService;
    }

    /// <summary>[SystemAdmin] Lấy thống kê số lượng công ty sử dụng các module</summary>
    [HttpGet("module-usage")]
    public async Task<IActionResult> GetModuleUsageStatistics([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _systemDashboardService.GetModuleUsageStatisticsAsync(month, year);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy thống kê số lượng công ty hủy gói đăng ký module</summary>
    [HttpGet("module-cancellations")]
    public async Task<IActionResult> GetModuleCancellationStatistics([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _systemDashboardService.GetModuleCancellationStatisticsAsync(month, year);
        return Ok(result);
    }
}
