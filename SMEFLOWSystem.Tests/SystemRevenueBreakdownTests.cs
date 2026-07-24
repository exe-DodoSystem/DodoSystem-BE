using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemRevenueBreakdownTests
{
    private static readonly DateOnly From = new(2026, 7, 1);
    private static readonly DateOnly To = new(2026, 7, 31);
    private static readonly DateTime ProcessedAt =
        new(2026, 7, 15, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ModuleDimension_AllocatesDiscountedMultiModuleOrderWithoutDoubleCount()
    {
        var repository = new FakeAnalyticsRepository();
        var orderId = Guid.NewGuid();
        repository.Payments.Add(Payment(orderId, Guid.NewGuid(), "Tenant A", "VNPay", 90m));
        repository.AllocationRows.AddRange(
        [
            Allocation(orderId, 1, "ATTENDANCE", "Attendance", 60m, 90m, 10m),
            Allocation(orderId, 2, "PAYROLL", "Payroll", 40m, 90m, 10m)
        ]);

        var result = await CreateService(repository).GetRevenueBreakdownAsync(
            Query(SystemAnalyticsDimension.Module));

        Assert.Equal(90m, result.TotalCollectedRevenue);
        Assert.Equal(90m, result.Items.Sum(item => item.CollectedRevenue));
        Assert.Equal(54m, result.Items.Single(item => item.Id == "ATTENDANCE").CollectedRevenue);
        Assert.Equal(36m, result.Items.Single(item => item.Id == "PAYROLL").CollectedRevenue);
        Assert.Null(result.Other);
    }

    [Fact]
    public async Task ModuleDimension_AssignsDecimalRemainderToLastModule()
    {
        var repository = new FakeAnalyticsRepository();
        var orderId = Guid.NewGuid();
        repository.Payments.Add(Payment(orderId, Guid.NewGuid(), "Tenant A", "VNPay", 100m));
        repository.AllocationRows.AddRange(
        [
            Allocation(orderId, 1, "A", "Module A", 1m, 100m),
            Allocation(orderId, 2, "B", "Module B", 1m, 100m),
            Allocation(orderId, 3, "C", "Module C", 1m, 100m)
        ]);

        var result = await CreateService(repository).GetRevenueBreakdownAsync(
            Query(SystemAnalyticsDimension.Module));

        Assert.Equal(33.33m, result.Items.Single(item => item.Id == "A").CollectedRevenue);
        Assert.Equal(33.33m, result.Items.Single(item => item.Id == "B").CollectedRevenue);
        Assert.Equal(33.34m, result.Items.Single(item => item.Id == "C").CollectedRevenue);
        Assert.Equal(result.TotalCollectedRevenue, result.Items.Sum(item => item.CollectedRevenue));
    }

    [Fact]
    public async Task TenantDimension_AppliesTopNAndRollsRemainderIntoOther()
    {
        var repository = new FakeAnalyticsRepository();
        for (var index = 1; index <= 6; index++)
        {
            repository.Payments.Add(Payment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Tenant {index}",
                "VNPay",
                index * 10m));
        }

        var query = Query(SystemAnalyticsDimension.Tenant);
        query.Limit = 5;
        var result = await CreateService(repository).GetRevenueBreakdownAsync(query);

        Assert.Equal(210m, result.TotalCollectedRevenue);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(10m, Assert.IsType<SystemRevenueBreakdownItemDto>(result.Other)
            .CollectedRevenue);
        Assert.Equal(
            result.TotalCollectedRevenue,
            result.Items.Sum(item => item.CollectedRevenue)
                + result.Other.CollectedRevenue);
    }

    [Fact]
    public async Task MissingModuleLines_PreservesRevenueInOtherAndAddsWarning()
    {
        var repository = new FakeAnalyticsRepository();
        repository.Payments.Add(Payment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tenant A",
            "VNPay",
            75m));

        var result = await CreateService(repository).GetRevenueBreakdownAsync(
            Query(SystemAnalyticsDimension.Module));

        Assert.Empty(result.Items);
        Assert.Equal(75m, Assert.IsType<SystemRevenueBreakdownItemDto>(result.Other)
            .CollectedRevenue);
        Assert.Contains(
            SystemAnalyticsWarningCodes.OrderModuleAllocationUnavailable,
            result.Meta.Warnings);
    }

    [Fact]
    public async Task EveryDimension_ReconcilesWithRevenueSeriesCollectedTotal()
    {
        var repository = new FakeAnalyticsRepository();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        repository.Payments.AddRange(
        [
            Payment(orderA, tenantA, "Tenant A", "VNPay", 100m),
            Payment(orderB, tenantB, "Tenant B", "MoMo", 50m)
        ]);
        repository.AllocationRows.AddRange(
        [
            Allocation(orderA, 1, "A", "Module A", 3m, 100m),
            Allocation(orderA, 2, "B", "Module B", 1m, 100m),
            Allocation(orderB, 2, "B", "Module B", 1m, 50m)
        ]);
        var service = CreateService(repository);
        var series = await service.GetRevenueSeriesAsync(new SystemRevenueSeriesQueryDto
        {
            From = From,
            To = To,
            Compare = SystemAnalyticsCompare.None,
            Granularity = SystemAnalyticsGranularity.Day
        });
        var seriesTotal = series.Points.Sum(point => point.CollectedRevenue);

        foreach (var dimension in new[]
                 {
                     SystemAnalyticsDimension.Module,
                     SystemAnalyticsDimension.Tenant,
                     SystemAnalyticsDimension.Gateway
                 })
        {
            var breakdown = await service.GetRevenueBreakdownAsync(Query(dimension));
            Assert.Equal(seriesTotal, breakdown.TotalCollectedRevenue);
            Assert.Equal(
                seriesTotal,
                breakdown.Items.Sum(item => item.CollectedRevenue)
                    + (breakdown.Other?.CollectedRevenue ?? 0m));
        }
    }

    private static SystemRevenueBreakdownQueryDto Query(string dimension)
    {
        return new SystemRevenueBreakdownQueryDto
        {
            From = From,
            To = To,
            Compare = SystemAnalyticsCompare.None,
            Dimension = dimension
        };
    }

    private static SystemAnalyticsService CreateService(FakeAnalyticsRepository repository)
    {
        return new SystemAnalyticsService(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SystemAnalyticsOptions()),
            NullLogger<SystemAnalyticsService>.Instance);
    }

    private static CollectedPaymentRow Payment(
        Guid orderId,
        Guid tenantId,
        string tenantName,
        string gateway,
        decimal amount)
    {
        return new CollectedPaymentRow
        {
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            TenantId = tenantId,
            TenantName = tenantName,
            Gateway = gateway,
            Status = "Success",
            Amount = amount,
            CreatedAt = ProcessedAt,
            ProcessedAt = ProcessedAt
        };
    }

    private static BillingOrderModuleAllocationRow Allocation(
        Guid orderId,
        int moduleId,
        string moduleCode,
        string moduleName,
        decimal lineTotal,
        decimal finalAmount,
        decimal discountAmount = 0m)
    {
        return new BillingOrderModuleAllocationRow
        {
            OrderId = orderId,
            ModuleId = moduleId,
            ModuleCode = moduleCode,
            ModuleName = moduleName,
            LineTotal = lineTotal,
            OrderFinalAmount = finalAmount,
            OrderDiscountAmount = discountAmount,
            PaymentStatus = "Paid"
        };
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public List<CollectedPaymentRow> Payments { get; } = [];
        public List<BillingOrderModuleAllocationRow> AllocationRows { get; } = [];

        public Task<List<CollectedPaymentRow>> GetRevenuePaymentsAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            int? moduleId,
            string tenantSegment,
            CancellationToken ct)
        {
            return Task.FromResult(Payments
                .Where(payment =>
                    payment.ProcessedAt >= fromUtc
                    && payment.ProcessedAt < toExclusiveUtc)
                .ToList());
        }

        public Task<List<BillingOrderModuleAllocationRow>>
            GetBillingOrderModuleAllocationsAsync(
                IReadOnlyCollection<Guid> orderIds,
                CancellationToken ct)
        {
            return Task.FromResult(AllocationRows
                .Where(row => orderIds.Contains(row.OrderId))
                .ToList());
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
