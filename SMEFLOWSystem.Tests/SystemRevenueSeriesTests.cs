using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemRevenueSeriesTests
{
    [Fact]
    public async Task EmptyRange_ReturnsEveryBucketWithZeroAndRequiredMetadata()
    {
        var repository = new FakeAnalyticsRepository();
        var service = CreateService(repository);

        var result = await service.GetRevenueSeriesAsync(Query(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3)));

        Assert.Equal(3, result.Points.Count);
        Assert.All(result.Points, point =>
        {
            Assert.Equal(0m, point.InvoicedRevenue);
            Assert.Equal(0m, point.CollectedRevenue);
            Assert.Equal(0m, point.OutstandingCreated);
            Assert.Equal(0m, point.MrrSnapshot);
            Assert.Null(point.RefundedAmount);
        });
        Assert.Null(result.PreviousPoints);
        Assert.Null(result.Meta.DataThrough);
        Assert.Equal(SystemAnalyticsMrrStatus.Estimated, result.Meta.MrrStatus);
        Assert.False(result.Meta.ExcludesTestTenants);
        Assert.Contains(SystemAnalyticsWarningCodes.RefundDataUnavailable, result.Meta.Warnings);
        Assert.Contains(SystemAnalyticsWarningCodes.TestTenantFlagUnavailable, result.Meta.Warnings);
        Assert.Contains(SystemAnalyticsWarningCodes.MrrUsesCurrentCatalogPrice, result.Meta.Warnings);
    }

    [Fact]
    public async Task LocalDayBoundary_UsesUtcPlusSevenAndExclusiveUpperBound()
    {
        var repository = new FakeAnalyticsRepository();
        repository.Payments.AddRange(
        [
            Payment(
                "Success",
                new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc),
                100m),
            Payment(
                "Paid",
                new DateTime(2026, 7, 1, 16, 59, 59, DateTimeKind.Utc),
                200m),
            Payment(
                "Success",
                new DateTime(2026, 7, 1, 17, 0, 0, DateTimeKind.Utc),
                400m)
        ]);
        var service = CreateService(repository);

        var result = await service.GetRevenueSeriesAsync(Query(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1)));

        var point = Assert.Single(result.Points);
        Assert.Equal(300m, point.CollectedRevenue);
        Assert.Equal("2026-07-01", point.BucketStart);
    }

    [Fact]
    public async Task PreviousPeriod_ReturnsSameBucketCountAndReconcilesTotals()
    {
        var repository = new FakeAnalyticsRepository();
        repository.Invoices.AddRange(
        [
            Invoice(new DateTime(2026, 7, 8, 5, 0, 0, DateTimeKind.Utc), 50m),
            Invoice(new DateTime(2026, 7, 10, 5, 0, 0, DateTimeKind.Utc), 100m),
            Invoice(new DateTime(2026, 7, 11, 5, 0, 0, DateTimeKind.Utc), 200m)
        ]);
        repository.Payments.AddRange(
        [
            Payment("Success", new DateTime(2026, 7, 8, 6, 0, 0, DateTimeKind.Utc), 25m),
            Payment("Success", new DateTime(2026, 7, 10, 6, 0, 0, DateTimeKind.Utc), 75m),
            Payment("Settled", new DateTime(2026, 7, 11, 6, 0, 0, DateTimeKind.Utc), 125m)
        ]);
        var service = CreateService(repository);
        var query = Query(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 11));
        query.Compare = SystemAnalyticsCompare.PreviousPeriod;

        var result = await service.GetRevenueSeriesAsync(query);

        Assert.Equal(2, result.Points.Count);
        Assert.NotNull(result.PreviousPoints);
        Assert.Equal(2, result.PreviousPoints.Count);
        Assert.Equal(300m, result.Points.Sum(point => point.InvoicedRevenue));
        Assert.Equal(200m, result.Points.Sum(point => point.CollectedRevenue));
        Assert.Equal(50m, result.PreviousPoints.Sum(point => point.InvoicedRevenue));
        Assert.Equal(25m, result.PreviousPoints.Sum(point => point.CollectedRevenue));
        Assert.Equal("2026-07-08", result.Meta.PreviousFrom);
        Assert.Equal("2026-07-09", result.Meta.PreviousTo);
    }

    [Fact]
    public async Task PaymentQuality_ExcludesInvalidRowsAndAddsWarnings()
    {
        var repository = new FakeAnalyticsRepository();
        var processedAt = new DateTime(2026, 7, 1, 5, 0, 0, DateTimeKind.Utc);
        repository.Payments.AddRange(
        [
            Payment("Success", processedAt, 100m),
            Payment("Failed", processedAt, 200m),
            Payment("AlienStatus", processedAt, 300m),
            Payment("Success", null, 400m, processedAt)
        ]);
        var service = CreateService(repository);

        var result = await service.GetRevenueSeriesAsync(Query(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1)));

        Assert.Equal(100m, Assert.Single(result.Points).CollectedRevenue);
        Assert.Contains(
            SystemAnalyticsWarningCodes.PaymentWithoutProcessedAtExcluded,
            result.Meta.Warnings);
        Assert.Contains(
            SystemAnalyticsWarningCodes.PaymentStatusUnrecognized,
            result.Meta.Warnings);
    }

    [Fact]
    public async Task EstimatedMrr_UsesSnapshotAtEachBucketEnd()
    {
        var repository = new FakeAnalyticsRepository();
        repository.Subscriptions.Add(new ActiveSubscriptionPriceRow
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ModuleId = 1,
            MonthlyPrice = 500m,
            StartDate = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            DataUpdatedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            Status = "Active"
        });
        var service = CreateService(repository);

        var result = await service.GetRevenueSeriesAsync(Query(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2)));

        Assert.Equal(0m, result.Points[0].MrrSnapshot);
        Assert.Equal(500m, result.Points[1].MrrSnapshot);
        Assert.Equal(
            new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            result.Meta.DataThrough);
    }

    [Fact]
    public async Task NormalizedQuery_IsCachedWithoutRepeatingRepositoryQueries()
    {
        var repository = new FakeAnalyticsRepository();
        var service = CreateService(repository);
        var query = Query(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));

        var first = await service.GetRevenueSeriesAsync(query);
        var second = await service.GetRevenueSeriesAsync(query);

        Assert.Same(first, second);
        Assert.Equal(1, repository.InvoicedQueryCount);
        Assert.Equal(1, repository.PaymentQueryCount);
        Assert.Equal(1, repository.OutstandingQueryCount);
        Assert.Equal(1, repository.SubscriptionQueryCount);
    }

    [Fact]
    public async Task UnknownModule_IsRejectedBeforeRevenueQueries()
    {
        var repository = new FakeAnalyticsRepository
        {
            ModuleExists = false
        };
        var service = CreateService(repository);
        var query = Query(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));
        query.ModuleId = 999;

        var exception = await Assert.ThrowsAsync<SystemAnalyticsQueryValidationException>(
            () => service.GetRevenueSeriesAsync(query));

        Assert.Contains(nameof(SystemRevenueSeriesQueryDto.ModuleId), exception.Errors.Keys);
        Assert.Equal(0, repository.InvoicedQueryCount);
    }

    private static SystemAnalyticsService CreateService(FakeAnalyticsRepository repository)
    {
        return new SystemAnalyticsService(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SystemAnalyticsOptions
            {
                CacheSeconds = 120,
                DefaultRangeDays = 30,
                MaxRangeMonths = 24
            }),
            NullLogger<SystemAnalyticsService>.Instance);
    }

    private static SystemRevenueSeriesQueryDto Query(DateOnly from, DateOnly to)
    {
        return new SystemRevenueSeriesQueryDto
        {
            From = from,
            To = to,
            Compare = SystemAnalyticsCompare.None,
            Granularity = SystemAnalyticsGranularity.Day
        };
    }

    private static InvoicedOrderRow Invoice(DateTime billingDate, decimal amount)
    {
        return new InvoicedOrderRow
        {
            OrderId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            BillingDate = billingDate,
            FinalAmount = amount,
            PaymentStatus = "Paid"
        };
    }

    private static CollectedPaymentRow Payment(
        string status,
        DateTime? processedAt,
        decimal amount,
        DateTime? createdAt = null)
    {
        return new CollectedPaymentRow
        {
            PaymentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Status = status,
            Gateway = "Test",
            CreatedAt = createdAt ?? processedAt ?? DateTime.UtcNow,
            ProcessedAt = processedAt,
            Amount = amount
        };
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public List<InvoicedOrderRow> Invoices { get; } = [];
        public List<CollectedPaymentRow> Payments { get; } = [];
        public List<OutstandingOrderRow> OutstandingOrders { get; } = [];
        public List<ActiveSubscriptionPriceRow> Subscriptions { get; } = [];
        public bool ModuleExists { get; set; } = true;
        public int InvoicedQueryCount { get; private set; }
        public int PaymentQueryCount { get; private set; }
        public int OutstandingQueryCount { get; private set; }
        public int SubscriptionQueryCount { get; private set; }

        public Task<List<InvoicedOrderRow>> GetInvoicedOrdersAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            InvoicedQueryCount++;
            return Task.FromResult(Invoices
                .Where(row => row.BillingDate >= fromUtc && row.BillingDate < toExclusiveUtc)
                .ToList());
        }

        public Task<List<CollectedPaymentRow>> GetRevenuePaymentsAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            PaymentQueryCount++;
            return Task.FromResult(Payments
                .Where(row =>
                    (row.ProcessedAt.HasValue
                        && row.ProcessedAt.Value >= fromUtc
                        && row.ProcessedAt.Value < toExclusiveUtc)
                    || (!row.ProcessedAt.HasValue
                        && row.CreatedAt >= fromUtc
                        && row.CreatedAt < toExclusiveUtc))
                .ToList());
        }

        public Task<List<OutstandingOrderRow>> GetPendingOutstandingOrdersAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            OutstandingQueryCount++;
            return Task.FromResult(OutstandingOrders
                .Where(row => row.CreatedAt >= fromUtc && row.CreatedAt < toExclusiveUtc)
                .ToList());
        }

        public Task<List<ActiveSubscriptionPriceRow>> GetActiveSubscriptionPricesAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            SubscriptionQueryCount++;
            return Task.FromResult(Subscriptions
                .Where(row => row.StartDate < toExclusiveUtc && row.EndDate > fromUtc)
                .ToList());
        }

        public Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct)
            => Task.FromResult(ModuleExists);

        public Task<decimal> GetEstimatedMrrAtAsync(
            DateTime asOfUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
            => Task.FromResult(0m);

        public Task<List<BillingOrderModuleAllocationRow>> GetBillingOrderModuleAllocationsAsync(
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
            CancellationToken ct)
            => Task.FromResult<TenantFinancialAggregateRow?>(null);

        public Task<TenantSubscriptionCountRow> GetTenantSubscriptionCountsAsync(
            Guid tenantId,
            DateTime nowUtc,
            CancellationToken ct)
            => Task.FromResult(new TenantSubscriptionCountRow());

        public Task<List<MonthlyCollectedRevenueRow>> GetMonthlyCollectedRevenueAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            CancellationToken ct)
            => Task.FromResult(new List<MonthlyCollectedRevenueRow>());
    }
}
