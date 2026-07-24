using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemDashboardService
{
    Task<SystemDashboardOverviewDto> GetOverviewAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default);

    Task<List<ModuleUsageStatDto>> GetModuleUsageStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default);

    Task<List<ModuleCancellationStatDto>> GetModuleCancellationStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default);

    Task<List<ModuleExpirationStatDto>> GetModuleExpirationStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default);

    Task<SystemModuleTrendDto> GetModuleTrendsAsync(
        SystemModuleTrendQueryDto query,
        CancellationToken cancellationToken = default);
}
