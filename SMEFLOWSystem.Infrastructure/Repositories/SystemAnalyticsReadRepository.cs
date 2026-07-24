using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemAnalyticsReadRepository : ISystemAnalyticsReadRepository
{
    private static readonly string[] SuccessfulPaymentStatuses =
    [
        "success",
        "succeeded",
        "settled",
        "paid"
    ];

    private readonly SMEFLOWSystemContext _context;

    public SystemAnalyticsReadRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public Task<List<InvoicedOrderRow>> GetInvoicedOrdersAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct)
    {
        ValidateRange(fromUtc, toExclusiveUtc);
        var segment = NormalizeTenantSegment(tenantSegment);
        var includeAllTenants = segment == SystemAnalyticsSegment.All;
        var includeTrialTenants = segment == SystemAnalyticsSegment.Trial;

        var lines = _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking();
        var query =
            from order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.BillingDate >= fromUtc
                && order.BillingDate < toExclusiveUtc
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (includeAllTenants
                    || (includeTrialTenants && tenant.Status.ToLower() == "trial")
                    || (!includeTrialTenants && tenant.Status.ToLower() != "trial"))
                && order.Status.ToLower() != "cancelled"
                && order.PaymentStatus.ToLower() != "cancelled"
                && (!moduleId.HasValue || lines.Any(line =>
                    line.BillingOrderId == order.Id
                    && line.TenantId == order.TenantId
                    && line.ModuleId == moduleId.Value))
            select new InvoicedOrderRow
            {
                OrderId = order.Id,
                TenantId = order.TenantId,
                BillingDate = order.BillingDate,
                FinalAmount = order.FinalAmount
                    ?? (order.TotalAmount - (order.DiscountAmount ?? 0m)),
                PaymentStatus = order.PaymentStatus
            };

        return query.ToListAsync(ct);
    }

    public Task<List<CollectedPaymentRow>> GetRevenuePaymentsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct)
    {
        ValidateRange(fromUtc, toExclusiveUtc);
        var segment = NormalizeTenantSegment(tenantSegment);
        var includeAllTenants = segment == SystemAnalyticsSegment.All;
        var includeTrialTenants = segment == SystemAnalyticsSegment.Trial;

        var lines = _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking();
        var query =
            from payment in _context.PaymentTransactions.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on payment.BillingOrderId equals order.Id
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on payment.TenantId equals tenant.Id
            where payment.TenantId == order.TenantId
                && ((payment.ProcessedAt.HasValue
                        && payment.ProcessedAt.Value >= fromUtc
                        && payment.ProcessedAt.Value < toExclusiveUtc)
                    || (!payment.ProcessedAt.HasValue
                        && payment.CreatedAt >= fromUtc
                        && payment.CreatedAt < toExclusiveUtc))
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (includeAllTenants
                    || (includeTrialTenants && tenant.Status.ToLower() == "trial")
                    || (!includeTrialTenants && tenant.Status.ToLower() != "trial"))
                && (!moduleId.HasValue || lines.Any(line =>
                    line.BillingOrderId == order.Id
                    && line.TenantId == order.TenantId
                    && line.ModuleId == moduleId.Value))
            select new CollectedPaymentRow
            {
                PaymentId = payment.Id,
                CreatedAt = payment.CreatedAt,
                ProcessedAt = payment.ProcessedAt,
                Amount = payment.Amount,
                Status = payment.Status,
                Gateway = payment.Gateway,
                TenantId = payment.TenantId,
                TenantName = tenant.Name,
                OrderId = payment.BillingOrderId
            };

        return query.ToListAsync(ct);
    }

    public Task<List<OutstandingOrderRow>> GetPendingOutstandingOrdersAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct)
    {
        ValidateRange(fromUtc, toExclusiveUtc);
        var segment = NormalizeTenantSegment(tenantSegment);
        var includeAllTenants = segment == SystemAnalyticsSegment.All;
        var includeTrialTenants = segment == SystemAnalyticsSegment.Trial;
        var lines = _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking();

        var query =
            from order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.CreatedAt.HasValue
                && order.CreatedAt.Value >= fromUtc
                && order.CreatedAt.Value < toExclusiveUtc
                && order.PaymentStatus.ToLower() == "pending"
                && order.Status.ToLower() != "cancelled"
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (includeAllTenants
                    || (includeTrialTenants && tenant.Status.ToLower() == "trial")
                    || (!includeTrialTenants && tenant.Status.ToLower() != "trial"))
                && (!moduleId.HasValue || lines.Any(line =>
                    line.BillingOrderId == order.Id
                    && line.TenantId == order.TenantId
                    && line.ModuleId == moduleId.Value))
            select new OutstandingOrderRow
            {
                OrderId = order.Id,
                TenantId = order.TenantId,
                CreatedAt = order.CreatedAt!.Value,
                FinalAmount = order.FinalAmount
                    ?? (order.TotalAmount - (order.DiscountAmount ?? 0m))
            };

        return query.ToListAsync(ct);
    }

    public async Task<decimal> GetEstimatedMrrAtAsync(
        DateTime asOfUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct)
    {
        var segment = NormalizeTenantSegment(tenantSegment);
        var includeAllTenants = segment == SystemAnalyticsSegment.All;
        var includeTrialTenants = segment == SystemAnalyticsSegment.Trial;

        return await (
            from subscription in _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            join module in _context.Modules.AsNoTracking()
                on subscription.ModuleId equals module.Id
            where subscription.StartDate <= asOfUtc
                && subscription.EndDate > asOfUtc
                && !subscription.IsDeleted
                && subscription.Status.ToLower() == "active"
                && (!moduleId.HasValue || subscription.ModuleId == moduleId.Value)
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (includeAllTenants
                    || (includeTrialTenants && tenant.Status.ToLower() == "trial")
                    || (!includeTrialTenants && tenant.Status.ToLower() != "trial"))
            select (decimal?)module.MonthlyPrice)
            .SumAsync(ct) ?? 0m;
    }

    public Task<List<ActiveSubscriptionPriceRow>> GetActiveSubscriptionPricesAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        CancellationToken ct)
    {
        ValidateRange(fromUtc, toExclusiveUtc);
        var segment = NormalizeTenantSegment(tenantSegment);
        var includeAllTenants = segment == SystemAnalyticsSegment.All;
        var includeTrialTenants = segment == SystemAnalyticsSegment.Trial;

        var query =
            from subscription in _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            join module in _context.Modules.AsNoTracking()
                on subscription.ModuleId equals module.Id
            where subscription.StartDate < toExclusiveUtc
                && subscription.EndDate > fromUtc
                && !subscription.IsDeleted
                && subscription.Status.ToLower() == "active"
                && (!moduleId.HasValue || subscription.ModuleId == moduleId.Value)
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (includeAllTenants
                    || (includeTrialTenants && tenant.Status.ToLower() == "trial")
                    || (!includeTrialTenants && tenant.Status.ToLower() != "trial"))
            select new ActiveSubscriptionPriceRow
            {
                SubscriptionId = subscription.Id,
                TenantId = subscription.TenantId,
                ModuleId = subscription.ModuleId,
                MonthlyPrice = module.MonthlyPrice,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                DataUpdatedAt = subscription.UpdatedAt ?? subscription.CreatedAt,
                Status = subscription.Status
            };

        return query.ToListAsync(ct);
    }

    public Task<bool> ModuleExistsAsync(int moduleId, CancellationToken ct)
    {
        return _context.Modules
            .AsNoTracking()
            .AnyAsync(module => module.Id == moduleId, ct);
    }

    public Task<List<BillingOrderModuleAllocationRow>> GetBillingOrderModuleAllocationsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        if (orderIds.Count == 0)
        {
            return Task.FromResult(new List<BillingOrderModuleAllocationRow>());
        }

        var distinctOrderIds = orderIds.Distinct().ToArray();

        var query =
            from line in _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on line.BillingOrderId equals order.Id
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            join module in _context.Modules.AsNoTracking()
                on line.ModuleId equals module.Id
            where line.TenantId == order.TenantId
                && distinctOrderIds.Contains(order.Id)
                && order.IsDeleted != true
                && order.Status.ToLower() != "cancelled"
                && order.PaymentStatus.ToLower() != "cancelled"
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new BillingOrderModuleAllocationRow
            {
                OrderId = order.Id,
                ModuleId = module.Id,
                ModuleCode = module.Code,
                ModuleName = module.Name,
                LineTotal = line.LineTotal,
                OrderFinalAmount = order.FinalAmount
                    ?? (order.TotalAmount - (order.DiscountAmount ?? 0m)),
                OrderDiscountAmount = order.DiscountAmount ?? 0m,
                PaymentStatus = order.PaymentStatus,
                TenantId = order.TenantId
            };

        return query.ToListAsync(ct);
    }

    public async Task<List<ActionCenterCandidateRow>> GetActionCenterCandidatesAsync(
        DateTime nowUtc,
        int overdueGraceHours,
        CancellationToken ct)
    {
        if (overdueGraceHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overdueGraceHours),
                "Overdue grace hours cannot be negative.");
        }

        var failedSinceUtc = nowUtc.AddHours(-24);
        var overdueBeforeUtc = nowUtc.AddHours(-overdueGraceHours);
        var expiringBeforeUtc = nowUtc.AddDays(7);

        var failedPayments = await (
            from payment in _context.PaymentTransactions.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on payment.BillingOrderId equals order.Id
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on payment.TenantId equals tenant.Id
            where payment.TenantId == order.TenantId
                && payment.Status.ToLower() == "failed"
                && (payment.ProcessedAt ?? payment.CreatedAt) >= failedSinceUtc
                && (payment.ProcessedAt ?? payment.CreatedAt) < nowUtc
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new ActionCenterCandidateRow
            {
                Type = SystemActionCenterItemType.PaymentFailed,
                EntityId = payment.Id,
                EntityName = order.BillingOrderNumber,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                OccurredAt = payment.ProcessedAt ?? payment.CreatedAt,
                AdditionalInfo = payment.Gateway
            })
            .ToListAsync(ct);

        var overdueOrders = await (
            from order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.PaymentStatus.ToLower() == "pending"
                && order.Status.ToLower() != "cancelled"
                && order.BillingDate <= overdueBeforeUtc
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new ActionCenterCandidateRow
            {
                Type = SystemActionCenterItemType.OrderOverdue,
                EntityId = order.Id,
                EntityName = order.BillingOrderNumber,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                OccurredAt = order.BillingDate,
                AdditionalInfo = null
            })
            .ToListAsync(ct);

        var expiringSubscriptions = await (
            from subscription in _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            join module in _context.Modules.AsNoTracking()
                on subscription.ModuleId equals module.Id
            where subscription.StartDate <= nowUtc
                && subscription.EndDate > nowUtc
                && subscription.EndDate <= expiringBeforeUtc
                && !subscription.IsDeleted
                && (subscription.Status.ToLower() == "active"
                    || subscription.Status.ToLower() == "trial")
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new ActionCenterCandidateRow
            {
                Type = subscription.Status.ToLower() == "trial"
                    ? SystemActionCenterItemType.TrialEnding
                    : SystemActionCenterItemType.SubscriptionExpiring,
                EntityId = subscription.Id,
                EntityName = module.Name,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                OccurredAt = subscription.EndDate,
                AdditionalInfo = module.Code
            })
            .ToListAsync(ct);

        var suspendedTenants = await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant =>
                !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && tenant.Status.ToLower() == "suspended")
            .Select(tenant => new ActionCenterCandidateRow
            {
                Type = SystemActionCenterItemType.TenantSuspended,
                EntityId = tenant.Id,
                EntityName = tenant.Name,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                OccurredAt = tenant.UpdatedAt ?? tenant.CreatedAt,
                AdditionalInfo = null
            })
            .ToListAsync(ct);

        return failedPayments
            .Concat(overdueOrders)
            .Concat(expiringSubscriptions)
            .Concat(suspendedTenants)
            .ToList();
    }

    public async Task<TenantFinancialAggregateRow?> GetTenantFinancialAggregateAsync(
        Guid tenantId,
        DateTime periodFromUtc,
        DateTime periodToExclusiveUtc,
        CancellationToken ct)
    {
        ValidateRange(periodFromUtc, periodToExclusiveUtc);

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item =>
                item.Id == tenantId
                && !item.IsDeleted
                && item.Name != SystemTenantConstants.Name)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Status
            })
            .SingleOrDefaultAsync(ct);

        if (tenant == null)
        {
            return null;
        }

        var payments =
            from payment in _context.PaymentTransactions.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on payment.BillingOrderId equals order.Id
            where payment.TenantId == tenantId
                && order.TenantId == tenantId
                && order.IsDeleted != true
            select new
            {
                payment.Amount,
                payment.Status,
                payment.ProcessedAt,
                order.BillingDate
            };

        var successfulPayments = payments.Where(payment =>
            payment.ProcessedAt.HasValue
            && SuccessfulPaymentStatuses.Contains(payment.Status.ToLower()));

        var lifetimeCollectedRevenue = await successfulPayments
            .Select(payment => (decimal?)payment.Amount)
            .SumAsync(ct) ?? 0m;

        var collectedRevenueInPeriod = await successfulPayments
            .Where(payment =>
                payment.ProcessedAt!.Value >= periodFromUtc
                && payment.ProcessedAt.Value < periodToExclusiveUtc)
            .Select(payment => (decimal?)payment.Amount)
            .SumAsync(ct) ?? 0m;

        var lastSuccessfulPaymentAt = await successfulPayments
            .MaxAsync(payment => payment.ProcessedAt, ct);

        var lastFailedPaymentAt = await payments
            .Where(payment =>
                payment.Status.ToLower() == "failed"
                && payment.ProcessedAt.HasValue)
            .MaxAsync(payment => payment.ProcessedAt, ct);

        var paymentDates = await successfulPayments
            .Select(payment => new
            {
                ProcessedAt = payment.ProcessedAt!.Value,
                payment.BillingDate
            })
            .ToListAsync(ct);

        var outstandingAmount = await _context.BillingOrders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.IsDeleted != true
                && order.PaymentStatus.ToLower() == "pending"
                && order.Status.ToLower() != "cancelled")
            .Select(order => (decimal?)(order.FinalAmount
                ?? (order.TotalAmount - (order.DiscountAmount ?? 0m))))
            .SumAsync(ct) ?? 0m;

        return new TenantFinancialAggregateRow
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Status = tenant.Status,
            LifetimeCollectedRevenue = lifetimeCollectedRevenue,
            CollectedRevenueInPeriod = collectedRevenueInPeriod,
            OutstandingAmount = outstandingAmount,
            LastSuccessfulPaymentAt = lastSuccessfulPaymentAt,
            LastFailedPaymentAt = lastFailedPaymentAt,
            PaymentDelayDaysList = paymentDates
                .Select(payment => (decimal)(payment.ProcessedAt - payment.BillingDate).TotalDays)
                .ToList()
        };
    }

    public async Task<TenantSubscriptionCountRow> GetTenantSubscriptionCountsAsync(
        Guid tenantId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var expiringBeforeUtc = nowUtc.AddDays(30);
        var query =
            from subscription in _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            where subscription.TenantId == tenantId
                && subscription.StartDate <= nowUtc
                && subscription.EndDate > nowUtc
                && !subscription.IsDeleted
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select subscription;

        return new TenantSubscriptionCountRow
        {
            Active = await query.CountAsync(
                subscription => subscription.Status.ToLower() == "active",
                ct),
            Trial = await query.CountAsync(
                subscription => subscription.Status.ToLower() == "trial",
                ct),
            ExpiringIn30Days = await query.CountAsync(
                subscription =>
                    (subscription.Status.ToLower() == "active"
                        || subscription.Status.ToLower() == "trial")
                    && subscription.EndDate <= expiringBeforeUtc,
                ct)
        };
    }

    public Task<List<MonthlyCollectedRevenueRow>> GetMonthlyCollectedRevenueAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct)
    {
        ValidateRange(fromUtc, toExclusiveUtc);

        var query =
            from payment in _context.PaymentTransactions.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on payment.BillingOrderId equals order.Id
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on payment.TenantId equals tenant.Id
            where payment.TenantId == order.TenantId
                && payment.ProcessedAt.HasValue
                && payment.ProcessedAt.Value >= fromUtc
                && payment.ProcessedAt.Value < toExclusiveUtc
                && SuccessfulPaymentStatuses.Contains(payment.Status.ToLower())
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            group payment by new
            {
                payment.ProcessedAt!.Value.Year,
                payment.ProcessedAt.Value.Month
            }
            into month
            orderby month.Key.Year, month.Key.Month
            select new MonthlyCollectedRevenueRow
            {
                Year = month.Key.Year,
                Month = month.Key.Month,
                CollectedRevenue = month.Sum(payment => payment.Amount)
            };

        return query.ToListAsync(ct);
    }

    private static void ValidateRange(DateTime fromUtc, DateTime toExclusiveUtc)
    {
        if (fromUtc >= toExclusiveUtc)
        {
            throw new ArgumentException(
                "The inclusive lower boundary must be before the exclusive upper boundary.");
        }
    }

    private static string NormalizeTenantSegment(string tenantSegment)
    {
        var normalized = tenantSegment?.Trim().ToLowerInvariant();
        return normalized switch
        {
            SystemAnalyticsSegment.All => SystemAnalyticsSegment.All,
            SystemAnalyticsSegment.Paid => SystemAnalyticsSegment.Paid,
            SystemAnalyticsSegment.Trial => SystemAnalyticsSegment.Trial,
            _ => throw new ArgumentException(
                "Tenant segment must be 'all', 'paid', or 'trial'.",
                nameof(tenantSegment))
        };
    }
}
