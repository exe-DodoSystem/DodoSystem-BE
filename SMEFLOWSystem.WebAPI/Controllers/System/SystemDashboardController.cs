using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
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

    /// <summary>[SystemAdmin] Lấy KPI tổng quan toàn hệ thống</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] SystemPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemDashboardService.GetOverviewAsync(
            query.Month,
            query.Year,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy thống kê số lượng công ty sử dụng các module</summary>
    [HttpGet("module-usage")]
    public async Task<IActionResult> GetModuleUsageStatistics(
        [FromQuery] SystemPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemDashboardService.GetModuleUsageStatisticsAsync(
            query.Month,
            query.Year,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy thống kê số lượng công ty hủy gói đăng ký module</summary>
    [HttpGet("module-cancellations")]
    public async Task<IActionResult> GetModuleCancellationStatistics(
        [FromQuery] SystemPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemDashboardService.GetModuleCancellationStatisticsAsync(
            query.Month,
            query.Year,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy số tenant có module hết hạn trong kỳ</summary>
    [HttpGet("module-expirations")]
    public async Task<IActionResult> GetModuleExpirationStatistics(
        [FromQuery] SystemPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemDashboardService.GetModuleExpirationStatisticsAsync(
            query.Month,
            query.Year,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy xu hướng sử dụng, hủy và hết hạn module</summary>
    [HttpGet("module-trends")]
    public async Task<IActionResult> GetModuleTrends(
        [FromQuery] SystemModuleTrendQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemDashboardService.GetModuleTrendsAsync(
            query,
            cancellationToken);
        return Ok(result);
    }
}
