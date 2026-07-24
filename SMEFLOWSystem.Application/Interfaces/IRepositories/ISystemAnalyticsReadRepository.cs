using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

// ─── Raw projections returned from the read repository ───────────────────────
// (Not EF entities — no navigation properties, no tracking.)

public sealed class InvoicedOrderRow
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime BillingDate { get; set; }
    public decimal FinalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class CollectedPaymentRow
{
    public Guid PaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
}

public sealed class OutstandingOrderRow
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal FinalAmount { get; set; }
}

public sealed class BillingOrderModuleAllocationRow
{
    public Guid OrderId { get; set; }
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public decimal LineTotal { get; set; }
    public decimal OrderFinalAmount { get; set; }
    public decimal OrderDiscountAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
}

public sealed class ActiveSubscriptionPriceRow
{
    public Guid SubscriptionId { get; set; }
    public Guid TenantId { get; set; }
    public int ModuleId { get; set; }
    public decimal MonthlyPrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime DataUpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ActionCenterCandidateRow
{
    public string Type { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? AdditionalInfo { get; set; }
}

public sealed class TenantFinancialAggregateRow
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal LifetimeCollectedRevenue { get; set; }
    public decimal CollectedRevenueInPeriod { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime? LastSuccessfulPaymentAt { get; set; }
    public DateTime? LastFailedPaymentAt { get; set; }
    public List<decimal> PaymentDelayDaysList { get; set; } = new();
}

public sealed class TenantSubscriptionCountRow
{
    public int Active { get; set; }
    public int Trial { get; set; }
    public int ExpiringIn30Days { get; set; }
}

public sealed class MonthlyCollectedRevenueRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal CollectedRevenue { get; set; }
}

// ─── Repository interface ─────────────────────────────────────────────────────

public interface ISystemAnalyticsReadRepository
{
    // Revenue Series
    Task<List<InvoicedOrderRow>> GetInvoicedOrdersAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct);

    Task<List<CollectedPaymentRow>> GetRevenuePaymentsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct);

    Task<List<OutstandingOrderRow>> GetPendingOutstandingOrdersAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct);

    // MRR snapshot
    Task<List<ActiveSubscriptionPriceRow>> GetActiveSubscriptionPricesAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct);

    Task<decimal> GetEstimatedMrrAtAsync(
        DateTime asOfUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct);

    Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct);

    // Revenue Breakdown
    Task<List<BillingOrderModuleAllocationRow>> GetBillingOrderModuleAllocationsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);

    // Action Center
    Task<List<ActionCenterCandidateRow>> GetActionCenterCandidatesAsync(
        DateTime nowUtc,
        int overdueGraceHours,
        CancellationToken ct);

    // Tenant Financial Summary
    Task<TenantFinancialAggregateRow?> GetTenantFinancialAggregateAsync(
        Guid tenantId,
        DateTime periodFromUtc,
        DateTime periodToExclusiveUtc,
        CancellationToken ct);

    Task<TenantSubscriptionCountRow> GetTenantSubscriptionCountsAsync(
        Guid tenantId,
        DateTime nowUtc,
        CancellationToken ct);

    // Forecast — monthly collected revenue
    Task<List<MonthlyCollectedRevenueRow>> GetMonthlyCollectedRevenueAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct);
}
