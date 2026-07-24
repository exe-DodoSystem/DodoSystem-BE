using System;
using System.Threading;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemAnalyticsService
{
    Task<SystemRevenueSeriesResponseDto> GetRevenueSeriesAsync(
        SystemRevenueSeriesQueryDto query,
        CancellationToken ct = default);

    Task<SystemRevenueBreakdownResponseDto> GetRevenueBreakdownAsync(
        SystemRevenueBreakdownQueryDto query,
        CancellationToken ct = default);

    Task<SystemActionCenterResponseDto> GetActionCenterAsync(
        CancellationToken ct = default);

    Task<SystemRevenueForecastResponseDto> GetRevenueForecastAsync(
        SystemRevenueForecastQueryDto query,
        CancellationToken ct = default);
}
