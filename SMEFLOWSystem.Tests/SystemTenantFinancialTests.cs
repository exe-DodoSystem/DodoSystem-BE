using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemTenantFinancialTests
{
    [Fact]
    public async Task Summary_MapsAmountsSubscriptionsEstimatedMrrAndDelayWarning()
    {
        var tenantId = Guid.NewGuid();
        var lastSuccess = new DateTime(
            2026,
            7,
            20,
            8,
            0,
            0,
            DateTimeKind.Utc);
        var lastFailure = lastSuccess.AddDays(-1);
        var repository = new FakeAnalyticsRepository
        {
            Aggregate = new TenantFinancialAggregateRow
            {
                TenantId = tenantId,
                TenantName = "Tenant A",
                Status = "Active",
                LifetimeCollectedRevenue = 1_000m,
                CollectedRevenueInPeriod = 500m,
                OutstandingAmount = 200m,
                LastSuccessfulPaymentAt = lastSuccess,
                LastFailedPaymentAt = lastFailure,
                PaymentDelayDaysList = [-1m, 1m, 3m]
            },
            SubscriptionCounts = new TenantSubscriptionCountRow
            {
                Active = 2,
                Trial = 1,
                ExpiringIn30Days = 2,
                EstimatedMrr = 400m
            }
        };
        var query = Query();
        query.ModuleId = 7;

        var result = await CreateService(repository)
            .GetTenantFinancialSummaryAsync(tenantId, query);

        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(400m, result.CurrentMrr);
        Assert.Equal(1_000m, result.LifetimeCollectedRevenue);
        Assert.Equal(500m, result.CollectedRevenueInPeriod);
        Assert.Equal(200m, result.OutstandingAmount);
        Assert.Equal(lastSuccess, result.LastSuccessfulPaymentAt);
        Assert.Equal(lastFailure, result.LastFailedPaymentAt);
        Assert.Equal(2d, result.AveragePaymentDelayDays);
        Assert.Equal(2, result.Subscriptions.Active);
        Assert.Equal(1, result.Subscriptions.Trial);
        Assert.Equal(2, result.Subscriptions.ExpiringIn30Days);
        Assert.Equal(SystemAnalyticsMrrStatus.Estimated, result.Meta.MrrStatus);
        Assert.Contains(
            SystemAnalyticsWarningCodes.MrrUsesCurrentCatalogPrice,
            result.Meta.Warnings);
        Assert.Contains(
            SystemAnalyticsWarningCodes.RefundDataUnavailable,
            result.Meta.Warnings);
        Assert.Contains(
            SystemAnalyticsWarningCodes.PaymentDelayDaysNegativeExcluded,
            result.Meta.Warnings);
        Assert.Equal(7, repository.ObservedModuleId);
    }

    [Fact]
    public async Task NoPayments_ReturnsZeroAmountsAndNullPaymentFields()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeAnalyticsRepository
        {
            Aggregate = new TenantFinancialAggregateRow
            {
                TenantId = tenantId,
                TenantName = "Tenant A",
                Status = "Active"
            }
        };

        var result = await CreateService(repository)
            .GetTenantFinancialSummaryAsync(tenantId, Query());

        Assert.Equal(0m, result.LifetimeCollectedRevenue);
        Assert.Equal(0m, result.CollectedRevenueInPeriod);
        Assert.Equal(0m, result.OutstandingAmount);
        Assert.Null(result.LastSuccessfulPaymentAt);
        Assert.Null(result.LastFailedPaymentAt);
        Assert.Null(result.AveragePaymentDelayDays);
        Assert.DoesNotContain(
            SystemAnalyticsWarningCodes.PaymentDelayDaysNegativeExcluded,
            result.Meta.Warnings);
    }

    [Fact]
    public async Task MissingDeletedOrSystemTenant_IsReportedAsNotFound()
    {
        var repository = new FakeAnalyticsRepository
        {
            Aggregate = null
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService(repository).GetTenantFinancialSummaryAsync(
                Guid.NewGuid(),
                Query()));

        Assert.Equal(0, repository.SubscriptionQueryCount);
    }

    [Fact]
    public async Task UnknownModule_IsRejectedBeforeTenantQueries()
    {
        var repository = new FakeAnalyticsRepository
        {
            ModuleExists = false
        };
        var query = Query();
        query.ModuleId = 999;

        var exception =
            await Assert.ThrowsAsync<SystemAnalyticsQueryValidationException>(
                () => CreateService(repository).GetTenantFinancialSummaryAsync(
                    Guid.NewGuid(),
                    query));

        Assert.Contains(nameof(query.ModuleId), exception.Errors.Keys);
        Assert.Equal(0, repository.AggregateQueryCount);
    }

    private static SystemAnalyticsPeriodQueryDto Query()
    {
        return new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 7, 1),
            To = new DateOnly(2026, 7, 31),
            Compare = SystemAnalyticsCompare.None
        };
    }

    private static SystemTenantAnalyticsService CreateService(
        FakeAnalyticsRepository repository)
    {
        return new SystemTenantAnalyticsService(
            repository,
            Options.Create(new SystemAnalyticsOptions()));
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public TenantFinancialAggregateRow? Aggregate { get; set; }
        public TenantSubscriptionCountRow SubscriptionCounts { get; set; } = new();
        public bool ModuleExists { get; set; } = true;
        public int AggregateQueryCount { get; private set; }
        public int SubscriptionQueryCount { get; private set; }
        public int? ObservedModuleId { get; private set; }

        public Task<TenantFinancialAggregateRow?> GetTenantFinancialAggregateAsync(
            Guid tenantId,
            DateTime periodFromUtc,
            DateTime periodToExclusiveUtc,
            int? moduleId,
            CancellationToken ct)
        {
            AggregateQueryCount++;
            ObservedModuleId = moduleId;
            return Task.FromResult(Aggregate);
        }

        public Task<TenantSubscriptionCountRow> GetTenantSubscriptionCountsAsync(
            Guid tenantId,
            DateTime nowUtc,
            int? moduleId,
            CancellationToken ct)
        {
            SubscriptionQueryCount++;
            ObservedModuleId = moduleId;
            return Task.FromResult(SubscriptionCounts);
        }

        public Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct)
            => Task.FromResult(ModuleExists);

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

        public Task<List<MonthlyCollectedRevenueRow>> GetMonthlyCollectedRevenueAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            CancellationToken ct)
            => Task.FromResult(new List<MonthlyCollectedRevenueRow>());
    }
}
