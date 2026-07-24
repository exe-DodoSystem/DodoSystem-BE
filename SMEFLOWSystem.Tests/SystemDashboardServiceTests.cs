using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemDashboardServiceTests
{
    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(1, 2019)]
    public async Task InvalidPeriod_IsRejected(int month, int year)
    {
        var service = new SystemDashboardService(new DashboardReadRepositoryStub());

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GetOverviewAsync(month, year));
    }

    [Fact]
    public async Task Usage_UsesExclusiveMonthlyRange()
    {
        var repository = new DashboardReadRepositoryStub();
        var service = new SystemDashboardService(repository);

        await service.GetModuleUsageStatisticsAsync(7, 2026);

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), repository.Start);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), repository.End);
    }

    private sealed class DashboardReadRepositoryStub : ISystemDashboardReadRepository
    {
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        public Task<SystemDashboardOverviewDto> GetOverviewAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            DateOnly today,
            CancellationToken cancellationToken)
            => Task.FromResult(new SystemDashboardOverviewDto());

        public Task<List<ModuleUsageStatDto>> GetModuleUsageAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
        {
            Start = periodStartUtc;
            End = periodEndUtc;
            return Task.FromResult(new List<ModuleUsageStatDto>());
        }

        public Task<List<ModuleCancellationStatDto>> GetModuleCancellationsAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ModuleCancellationStatDto>());

        public Task<List<ModuleExpirationStatDto>> GetModuleExpirationsAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ModuleExpirationStatDto>());

        public Task<SystemModuleTrendDto> GetModuleTrendsAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new SystemModuleTrendDto());
    }
}
