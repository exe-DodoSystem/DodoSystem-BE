using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ISystemDashboardReadRepository
{
    Task<SystemDashboardOverviewDto> GetOverviewAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<List<ModuleUsageStatDto>> GetModuleUsageAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken);

    Task<List<ModuleCancellationStatDto>> GetModuleCancellationsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken);

    Task<List<ModuleExpirationStatDto>> GetModuleExpirationsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken);

    Task<SystemModuleTrendDto> GetModuleTrendsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken);
}
