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
    public async Task FiveActionTypes_MapToExpectedSeverityWarningsAndAllowListedPaths()
    {
        var now = DateTime.UtcNow;
        var tenantId = Guid.NewGuid();
        var repository = new FakeAnalyticsRepository();
        repository.Candidates.AddRange(
        [
            Candidate(SystemActionCenterItemType.PaymentFailed, tenantId, now.AddHours(-1)),
            Candidate(SystemActionCenterItemType.OrderOverdue, tenantId, now.AddHours(-2)),
            Candidate(SystemActionCenterItemType.SubscriptionExpiring, tenantId, now.AddDays(2)),
            Candidate(SystemActionCenterItemType.TrialEnding, tenantId, now.AddDays(3)),
            Candidate(SystemActionCenterItemType.TenantSuspended, tenantId, now.AddHours(-3))
        ]);

        var result = await CreateService(repository).GetActionCenterAsync();

        Assert.Equal(2, result.Counts.Critical);
        Assert.Equal(2, result.Counts.Warning);
        Assert.Equal(1, result.Counts.Info);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal($"{item.Type}_{item.EntityId:D}", item.Id);
            Assert.NotNull(item.TargetPath);
            Assert.StartsWith("/system-admin/", item.TargetPath, StringComparison.Ordinal);
            Assert.False(Uri.IsWellFormedUriString(item.TargetPath, UriKind.Absolute));
        });
        Assert.Equal(
            SystemActionCenterSeverity.Critical,
            result.Items.Single(item =>
                item.Type == SystemActionCenterItemType.PaymentFailed).Severity);
        Assert.Equal(
            SystemActionCenterSeverity.Warning,
            result.Items.Single(item =>
                item.Type == SystemActionCenterItemType.TrialEnding).Severity);
        Assert.Equal(
            SystemActionCenterSeverity.Info,
            result.Items.Single(item =>
                item.Type == SystemActionCenterItemType.TenantSuspended).Severity);
        Assert.Contains(
            SystemAnalyticsWarningCodes.OrderOverdueUsesConfiguredGracePeriod,
            result.Meta.Warnings);
        Assert.Contains(
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable,
            result.Meta.Warnings);
        Assert.Equal(SystemAnalyticsMrrStatus.Unavailable, result.Meta.MrrStatus);
        Assert.Equal(24, repository.ObservedOverdueGraceHours);
    }

    [Fact]
    public async Task DuplicateCandidates_AreCollapsedBeforeCountsSortingAndLimit()
    {
        var now = DateTime.UtcNow;
        var tenantId = Guid.NewGuid();
        var duplicateEntityId = Guid.NewGuid();
        var repository = new FakeAnalyticsRepository();
        repository.Candidates.AddRange(
        [
            Candidate(
                "paymentfailed",
                tenantId,
                now.AddHours(-4),
                duplicateEntityId),
            Candidate(
                SystemActionCenterItemType.PaymentFailed,
                tenantId,
                now.AddHours(-1),
                duplicateEntityId),
            Candidate(SystemActionCenterItemType.OrderOverdue, tenantId, now.AddHours(-2)),
            Candidate(SystemActionCenterItemType.TrialEnding, tenantId, now.AddDays(1)),
            Candidate(SystemActionCenterItemType.TenantSuspended, tenantId, now.AddHours(-3))
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
            result.Items.Single(item => item.EntityId == duplicateEntityId).OccurredAt);
        Assert.Equal(
            result.Items.Count,
            result.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task UnknownTypesAreExcludedAndLabelsDoNotPreserveControlCharacters()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeAnalyticsRepository();
        repository.Candidates.Add(new ActionCenterCandidateRow
        {
            Type = SystemActionCenterItemType.OrderOverdue,
            EntityId = Guid.NewGuid(),
            EntityName = "BO-1\r\nInjected",
            TenantId = tenantId,
            TenantName = "Tenant\u0000A",
            OccurredAt = DateTime.UtcNow.AddHours(-2)
        });
        repository.Candidates.Add(Candidate(
            "UnknownType",
            tenantId,
            DateTime.UtcNow));

        var result = await CreateService(repository).GetActionCenterAsync();

        var item = Assert.Single(result.Items);
        Assert.DoesNotContain('\r', item.Description);
        Assert.DoesNotContain('\n', item.Description);
        Assert.DoesNotContain('\0', item.Description);
        Assert.Equal(1, result.Counts.Critical);
        Assert.Equal(0, result.Counts.Warning);
        Assert.Equal(0, result.Counts.Info);
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

    private static ActionCenterCandidateRow Candidate(
        string type,
        Guid tenantId,
        DateTime occurredAt,
        Guid? entityId = null)
    {
        return new ActionCenterCandidateRow
        {
            Type = type,
            EntityId = entityId ?? Guid.NewGuid(),
            EntityName = "Entity A",
            TenantId = tenantId,
            TenantName = "Tenant A",
            OccurredAt = occurredAt
        };
    }

    private sealed class FakeAnalyticsRepository : ISystemAnalyticsReadRepository
    {
        public List<ActionCenterCandidateRow> Candidates { get; } = [];
        public int? ObservedOverdueGraceHours { get; private set; }

        public Task<List<ActionCenterCandidateRow>> GetActionCenterCandidatesAsync(
            DateTime nowUtc,
            int overdueGraceHours,
            CancellationToken ct)
        {
            ObservedOverdueGraceHours = overdueGraceHours;
            return Task.FromResult(Candidates.ToList());
        }

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
