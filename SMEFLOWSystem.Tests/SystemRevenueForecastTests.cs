using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemRevenueForecastTests
{
    [Fact]
    public void ZeroSeries_ReturnsZeroForecastAndBounds()
    {
        var calculation = RevenueForecastCalculator.Calculate(
            Points(0m, 0m, 0m, 0m, 0m, 0m),
            3);

        Assert.Equal(3, calculation.Points.Count);
        Assert.All(calculation.Points, point =>
        {
            Assert.Equal(0m, point.Value);
            Assert.Equal(0m, point.LowerBound);
            Assert.Equal(0m, point.UpperBound);
        });
    }

    [Fact]
    public void IncreasingSeries_ContinuesDeterministicLinearTrend()
    {
        var input = Points(100m, 200m, 300m, 400m, 500m, 600m);

        var first = RevenueForecastCalculator.Calculate(input, 2);
        var second = RevenueForecastCalculator.Calculate(input, 2);

        Assert.Equal(100m, first.Slope);
        Assert.Equal([700m, 800m],
            first.Points.Select(point => point.Value).ToArray());
        Assert.Equal(first.Slope, second.Slope);
        Assert.Equal(first.Intercept, second.Intercept);
        Assert.Equal(
            first.Points.ToArray(),
            second.Points.ToArray());
        Assert.All(first.Points, point =>
        {
            Assert.Equal(point.Value, point.LowerBound);
            Assert.Equal(point.Value, point.UpperBound);
        });
    }

    [Fact]
    public void DecreasingSeries_ClampsNegativeForecastToZero()
    {
        var calculation = RevenueForecastCalculator.Calculate(
            Points(500m, 400m, 300m, 200m, 100m, 0m),
            3);

        Assert.All(calculation.Points, point =>
        {
            Assert.True(point.Value >= 0m);
            Assert.True(point.LowerBound >= 0m);
            Assert.True(point.UpperBound >= point.Value);
        });
        Assert.Equal(0m, calculation.Points[0].Value);
    }

    [Fact]
    public void ResidualSeries_ReturnsValidConfidenceInterval()
    {
        var calculation = RevenueForecastCalculator.Calculate(
            Points(100m, 180m, 310m, 390m, 530m, 580m),
            3);

        Assert.True(calculation.ResidualStandardError > 0m);
        Assert.All(calculation.Points, point =>
        {
            Assert.True(point.LowerBound >= 0m);
            Assert.True(point.LowerBound <= point.Value);
            Assert.True(point.UpperBound >= point.Value);
            Assert.True(point.UpperBound > point.LowerBound);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void ForecastPeriodsOutsideContractAreRejected(int periods)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RevenueForecastCalculator.Calculate(
                Points(1m, 2m, 3m, 4m, 5m, 6m),
                periods));
    }

    [Fact]
    public async Task Service_WithFewerThanSixCompleteMonths_ReturnsInsufficientHistory()
    {
        var repository = new FakeAnalyticsRepository();
        var query = Query(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 5, 31));

        var exception =
            await Assert.ThrowsAsync<InsufficientForecastHistoryException>(() =>
                CreateService(repository).GetRevenueForecastAsync(query));

        Assert.Equal(6, exception.RequiredMonths);
        Assert.Equal(5, exception.AvailableMonths);
        Assert.Equal(0, repository.MonthlyQueryCount);
    }

    [Fact]
    public async Task Service_DoesNotFillMissingTrainingMonthWithFakeZero()
    {
        var repository = new FakeAnalyticsRepository();
        repository.MonthlyRows.AddRange(
        [
            Month(2026, 1, 100m),
            Month(2026, 2, 200m),
            Month(2026, 3, 300m),
            Month(2026, 5, 500m),
            Month(2026, 6, 600m)
        ]);

        var exception =
            await Assert.ThrowsAsync<InsufficientForecastHistoryException>(() =>
                CreateService(repository).GetRevenueForecastAsync(Query(
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 6, 30))));

        Assert.Equal(6, exception.RequiredMonths);
        Assert.Equal(5, exception.AvailableMonths);
    }

    [Fact]
    public async Task Service_ReturnsSeparatedActualForecastMetadataAndWarnings()
    {
        var repository = new FakeAnalyticsRepository();
        repository.MonthlyRows.AddRange(
        [
            Month(2026, 1, 100m),
            Month(2026, 2, 200m),
            Month(2026, 3, 300m),
            Month(2026, 4, 400m),
            Month(2026, 5, 500m),
            Month(2026, 6, 600m)
        ]);
        var query = Query(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        query.ForecastPeriods = 2;
        query.ModuleId = 7;
        query.TenantSegment = SystemAnalyticsSegment.Paid;

        var result = await CreateService(repository)
            .GetRevenueForecastAsync(query);

        Assert.Equal("LinearTrend", result.Method);
        Assert.Equal("2026-01-01", result.TrainingFrom);
        Assert.Equal("2026-06-30", result.TrainingTo);
        Assert.Equal(6, result.ActualPoints.Count);
        Assert.Equal(2, result.ForecastPoints.Count);
        Assert.Equal("2026-07-01", result.ForecastPoints[0].BucketStart);
        Assert.Equal("2026-08-01", result.ForecastPoints[1].BucketStart);
        Assert.Equal("2026-08-31", result.Meta.To);
        Assert.Equal(SystemAnalyticsMrrStatus.Unavailable, result.Meta.MrrStatus);
        Assert.Contains(
            SystemAnalyticsWarningCodes.ForecastExcludesRefunds,
            result.Meta.Warnings);
        Assert.Contains(
            SystemAnalyticsWarningCodes.ForecastBasedOnAvailablePaymentHistory,
            result.Meta.Warnings);
        Assert.Equal(7, repository.ObservedModuleId);
        Assert.Equal(SystemAnalyticsSegment.Paid, repository.ObservedTenantSegment);
    }

    private static IReadOnlyList<RevenueForecastInputPoint> Points(
        params decimal[] values)
    {
        var firstMonth = new DateOnly(2026, 1, 1);
        return values
            .Select((value, index) => new RevenueForecastInputPoint(
                firstMonth.AddMonths(index),
                value))
            .ToList();
    }

    private static MonthlyCollectedRevenueRow Month(
        int year,
        int month,
        decimal value)
    {
        return new MonthlyCollectedRevenueRow
        {
            Year = year,
            Month = month,
            CollectedRevenue = value
        };
    }

    private static SystemRevenueForecastQueryDto Query(DateOnly from, DateOnly to)
    {
        return new SystemRevenueForecastQueryDto
        {
            From = from,
            To = to,
            Compare = SystemAnalyticsCompare.None,
            Granularity = SystemAnalyticsGranularity.Month
        };
    }

    private static SystemAnalyticsService CreateService(
        FakeAnalyticsRepository repository)
    {
        return new SystemAnalyticsService(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SystemAnalyticsOptions
            {
                ForecastMinimumMonths = 6,
                ForecastMaximumPeriods = 6
            }),
            NullLogger<SystemAnalyticsService>.Instance);
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public List<MonthlyCollectedRevenueRow> MonthlyRows { get; } = [];
        public int MonthlyQueryCount { get; private set; }
        public int? ObservedModuleId { get; private set; }
        public string? ObservedTenantSegment { get; private set; }

        public Task<List<MonthlyCollectedRevenueRow>> GetMonthlyCollectedRevenueAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            MonthlyQueryCount++;
            ObservedModuleId = moduleId;
            ObservedTenantSegment = tenantSegment;
            return Task.FromResult(MonthlyRows.ToList());
        }

        public Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct)
            => Task.FromResult(true);

        public Task<List<InvoicedOrderRow>> GetInvoicedOrdersAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(new List<InvoicedOrderRow>());

        public Task<List<CollectedPaymentRow>> GetRevenuePaymentsAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(new List<CollectedPaymentRow>());

        public Task<List<OutstandingOrderRow>> GetPendingOutstandingOrdersAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(new List<OutstandingOrderRow>());

        public Task<List<ActiveSubscriptionPriceRow>> GetActiveSubscriptionPricesAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(new List<ActiveSubscriptionPriceRow>());

        public Task<decimal> GetEstimatedMrrAtAsync(
            DateTime asOfUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(0m);

        public Task<List<BillingOrderModuleAllocationRow>>
            GetBillingOrderModuleAllocationsAsync(
                IReadOnlyCollection<Guid> orderIds,
                CancellationToken ct)
            => Task.FromResult(new List<BillingOrderModuleAllocationRow>());

        public Task<List<ActionCenterCandidateRow>> GetActionCenterCandidatesAsync(
            DateTime nowUtc,
            int overdueGraceHours,
            CancellationToken ct)
            => Task.FromResult(new List<ActionCenterCandidateRow>());

        public Task<TenantFinancialAggregateRow?> GetTenantFinancialAggregateAsync(
            Guid tenantId,
            DateTime periodFromUtc,
            DateTime periodToExclusiveUtc,
            int? moduleId,
            CancellationToken ct)
            => Task.FromResult<TenantFinancialAggregateRow?>(null);

        public Task<TenantSubscriptionCountRow> GetTenantSubscriptionCountsAsync(
            Guid tenantId,
            DateTime nowUtc,
            int? moduleId,
            CancellationToken ct)
            => Task.FromResult(new TenantSubscriptionCountRow());
    }
}
