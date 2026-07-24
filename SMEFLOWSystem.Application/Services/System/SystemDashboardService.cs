using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.Application.Services.System;

public class SystemDashboardService : ISystemDashboardService
{
    private readonly ISystemDashboardReadRepository _readRepository;

    public SystemDashboardService(ISystemDashboardReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public Task<SystemDashboardOverviewDto> GetOverviewAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var period = GetPeriod(month, year);
        return _readRepository.GetOverviewAsync(
            period.Start,
            period.End,
            DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);
    }

    public Task<List<ModuleUsageStatDto>> GetModuleUsageStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var period = GetPeriod(month, year);
        return _readRepository.GetModuleUsageAsync(period.Start, period.End, cancellationToken);
    }

    public Task<List<ModuleCancellationStatDto>> GetModuleCancellationStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var period = GetPeriod(month, year);
        return _readRepository.GetModuleCancellationsAsync(period.Start, period.End, cancellationToken);
    }

    public Task<List<ModuleExpirationStatDto>> GetModuleExpirationStatisticsAsync(
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var period = GetPeriod(month, year);
        return _readRepository.GetModuleExpirationsAsync(period.Start, period.End, cancellationToken);
    }

    public Task<SystemModuleTrendDto> GetModuleTrendsAsync(
        SystemModuleTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var from = GetRequiredMonth(query.FromMonth, query.FromYear, "from");
        var to = GetRequiredMonth(query.ToMonth, query.ToYear, "to");
        if (from > to)
            throw new ArgumentException("The trend start month must not be later than the end month.");

        var monthCount = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
        if (monthCount > 24)
            throw new ArgumentException("The trend range cannot exceed 24 months.");

        return _readRepository.GetModuleTrendsAsync(from, to.AddMonths(1), cancellationToken);
    }

    private static (DateTime Start, DateTime End) GetPeriod(int? month, int? year)
    {
        var now = DateTime.UtcNow;
        var targetMonth = month ?? now.Month;
        var targetYear = year ?? now.Year;

        if (targetMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        if (targetYear < 2020 || targetYear > now.Year + 1)
            throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between 2020 and {now.Year + 1}.");

        var start = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private static DateTime GetRequiredMonth(int month, int year, string parameterName)
    {
        var now = DateTime.UtcNow;
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(parameterName, "Month must be between 1 and 12.");
        if (year < 2020 || year > now.Year + 1)
            throw new ArgumentOutOfRangeException(parameterName, $"Year must be between 2020 and {now.Year + 1}.");
        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
