using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemActionCenterTests
{
    [Fact]
    public async Task FiveTypes_MapToSeverityWarningsAndAllowListedPaths()
    {
        var now = DateTime.UtcNow;
        var repository = new FakeAnalyticsRepository();
        foreach (var type in new[]
                 {
                     SystemActionCenterItemType.PaymentFailed,
                     SystemActionCenterItemType.OrderOverdue,
                     SystemActionCenterItemType.SubscriptionExpiring,
                     SystemActionCenterItemType.TrialEnding,
                     SystemActionCenterItemType.TenantSuspended
                 })
        {
            repository.Candidates.Add(Candidate(type, now));
        }

        var result = await CreateService(repository).GetActionCenterAsync();

        Assert.Equal(2, result.Counts.Critical);
        Assert.Equal(2, result.Counts.Warning);
        Assert.Equal(1, result.Counts.Info);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal($"{item.Type}_{item.EntityId:D}", item.Id);
            Assert.StartsWith("/system-admin/", item.TargetPath, StringComparison.Ordinal);
            Assert.False(Uri.IsWellFormedUriString(
                item.TargetPath,
                UriKind.Absolute));
        });
        Assert.Contains(
            SystemAnalyticsWarningCodes.OrderOverdueUsesConfiguredGracePeriod,
            result.Meta.Warnings);
    }

    [Fact]
    public async Task DeduplicationAndLimit_DoNotReduceCounts()
    {
        var now = DateTime.UtcNow;
        var duplicateId = Guid.NewGuid();
        var repository = new FakeAnalyticsRepository();
        repository.Candidates.AddRange(
        [
            Candidate(
                SystemActionCenterItemType.PaymentFailed,
                now.AddHours(-2),
                duplicateId),
            Candidate(
                "paymentfailed",
                now.AddHours(-1),
                duplicateId),
            Candidate(SystemActionCenterItemType.OrderOverdue, now.AddHours(-3)),
            Candidate(SystemActionCenterItemType.TrialEnding, now.AddDays(1)),
            Candidate(SystemActionCenterItemType.TenantSuspended, now.AddHours(-4))
        ]);

        var result = await CreateService(repository, maxItems: 2)
            .GetActionCenterAsync();

        Assert.Equal(2, result.Counts.Critical);
        Assert.Equal(1, result.Counts.Warning);
        Assert.Equal(1, result.Counts.Info);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
            Assert.Equal(SystemActionCenterSeverity.Critical, item.Severity));
        Assert.Equal(
            now.AddHours(-1),
            result.Items.Single(item => item.EntityId == duplicateId).OccurredAt);
    }

    private static ActionCenterCandidateRow Candidate(
        string type,
        DateTime occurredAt,
        Guid? entityId = null)
    {
        return new ActionCenterCandidateRow
        {
            Type = type,
            EntityId = entityId ?? Guid.NewGuid(),
            EntityName = "Entity A",
            TenantId = Guid.NewGuid(),
            TenantName = "Tenant A",
            OccurredAt = occurredAt
        };
    }

    private static SystemAnalyticsService CreateService(
        FakeAnalyticsRepository repository,
        int maxItems = 100)
    {
        return new SystemAnalyticsService(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SystemAnalyticsOptions
            {
                OrderOverdueGraceHours = 24,
                ActionCenterMaxItems = maxItems
            }),
            NullLogger<SystemAnalyticsService>.Instance);
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public List<ActionCenterCandidateRow> Candidates { get; } = [];

        public Task<List<ActionCenterCandidateRow>> GetActionCenterCandidatesAsync(
            DateTime nowUtc,
            int overdueGraceHours,
            CancellationToken ct)
            => Task.FromResult(Candidates.ToList());

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

        public Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct)
            => Task.FromResult(true);

        public Task<List<BillingOrderModuleAllocationRow>>
            GetBillingOrderModuleAllocationsAsync(
                IReadOnlyCollection<Guid> orderIds,
                CancellationToken ct)
            => Task.FromResult(new List<BillingOrderModuleAllocationRow>());

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
