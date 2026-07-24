using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemDashboardReadRepository : ISystemDashboardReadRepository
{
    private readonly SMEFLOWSystemContext _context;

    public SystemDashboardReadRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<SystemDashboardOverviewDto> GetOverviewAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var tenants = _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Name != SystemTenantConstants.Name);

        var tenantIds = tenants.Select(t => t.Id);
        var subscriptions = _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId));
        var nowUtc = DateTime.UtcNow;

        var result = new SystemDashboardOverviewDto
        {
            Month = periodStartUtc.Month,
            Year = periodStartUtc.Year,
            TotalTenants = await tenants.CountAsync(cancellationToken),
            ActiveTenants = await tenants.CountAsync(t => t.Status == StatusEnum.TenantActive, cancellationToken),
            TrialTenants = await tenants.CountAsync(t => t.Status == StatusEnum.TenantTrial, cancellationToken),
            PendingPaymentTenants = await tenants.CountAsync(t => t.Status == StatusEnum.TenantPending, cancellationToken),
            SuspendedTenants = await tenants.CountAsync(t => t.Status == StatusEnum.TenantSuspended, cancellationToken),
            NewTenantsInPeriod = await tenants.CountAsync(
                t => t.CreatedAt >= periodStartUtc && t.CreatedAt < periodEndUtc,
                cancellationToken),
            ExpiringIn7Days = await tenants.CountAsync(
                t => t.SubscriptionEndDate.HasValue
                    && t.SubscriptionEndDate.Value >= today
                    && t.SubscriptionEndDate.Value <= today.AddDays(7),
                cancellationToken),
            ExpiringIn30Days = await tenants.CountAsync(
                t => t.SubscriptionEndDate.HasValue
                    && t.SubscriptionEndDate.Value >= today
                    && t.SubscriptionEndDate.Value <= today.AddDays(30),
                cancellationToken),
            ActiveSubscriptions = await subscriptions.CountAsync(
                s => !s.IsDeleted
                    && (s.Status == StatusEnum.ModuleActive || s.Status == StatusEnum.ModuleTrial)
                    && s.StartDate <= nowUtc
                    && s.EndDate > nowUtc,
                cancellationToken),
            CancelledSubscriptionsInPeriod = await subscriptions.CountAsync(
                s => s.IsDeleted
                    && s.Status == StatusEnum.ModuleSuspended
                    && s.UpdatedAt >= periodStartUtc
                    && s.UpdatedAt < periodEndUtc,
                cancellationToken),
            PendingBillingOrders = await _context.BillingOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(
                    o => o.IsDeleted != true
                        && tenantIds.Contains(o.TenantId)
                        && o.PaymentStatus == StatusEnum.PaymentPending,
                    cancellationToken),
            FailedPaymentsInPeriod = await _context.PaymentTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(
                    p => tenantIds.Contains(p.TenantId)
                        && p.Status == StatusEnum.PaymentFailed
                        && p.CreatedAt >= periodStartUtc
                        && p.CreatedAt < periodEndUtc,
                    cancellationToken)
        };

        return result;
    }

    public Task<List<ModuleUsageStatDto>> GetModuleUsageAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        var customerTenantIds = CustomerTenantIds();

        return _context.Modules
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => new ModuleUsageStatDto
            {
                Month = periodStartUtc.Month,
                Year = periodStartUtc.Year,
                ModuleId = m.Id,
                ModuleName = m.Name,
                ActiveCompaniesCount = _context.ModuleSubscriptions
                    .IgnoreQueryFilters()
                    .Where(s => s.ModuleId == m.Id
                        && customerTenantIds.Contains(s.TenantId)
                        && !s.IsDeleted
                        && (s.Status == StatusEnum.ModuleActive || s.Status == StatusEnum.ModuleTrial)
                        && s.StartDate < periodEndUtc
                        && s.EndDate >= periodStartUtc)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<ModuleCancellationStatDto>> GetModuleCancellationsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        var customerTenantIds = CustomerTenantIds();

        return _context.Modules
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => new ModuleCancellationStatDto
            {
                Month = periodStartUtc.Month,
                Year = periodStartUtc.Year,
                ModuleId = m.Id,
                ModuleName = m.Name,
                CancelledCompaniesCount = _context.ModuleSubscriptions
                    .IgnoreQueryFilters()
                    .Where(s => s.ModuleId == m.Id
                        && customerTenantIds.Contains(s.TenantId)
                        && s.IsDeleted
                        && s.Status == StatusEnum.ModuleSuspended
                        && s.UpdatedAt >= periodStartUtc
                        && s.UpdatedAt < periodEndUtc)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<ModuleExpirationStatDto>> GetModuleExpirationsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        var customerTenantIds = CustomerTenantIds();

        return _context.Modules
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => new ModuleExpirationStatDto
            {
                Month = periodStartUtc.Month,
                Year = periodStartUtc.Year,
                ModuleId = m.Id,
                ModuleName = m.Name,
                ExpiredCompaniesCount = _context.ModuleSubscriptions
                    .IgnoreQueryFilters()
                    .Where(s => s.ModuleId == m.Id
                        && customerTenantIds.Contains(s.TenantId)
                        && !s.IsDeleted
                        && s.EndDate >= periodStartUtc
                        && s.EndDate < periodEndUtc)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SystemModuleTrendDto> GetModuleTrendsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Database
            .SqlQuery<ModuleTrendRow>($"""
                WITH months AS (
                    SELECT generate_series(
                        {fromUtc},
                        {toExclusiveUtc} - INTERVAL '1 month',
                        INTERVAL '1 month') AS "MonthStart"
                ),
                customer_subscriptions AS (
                    SELECT subscription.*
                    FROM "ModuleSubscriptions" AS subscription
                    INNER JOIN "Tenants" AS tenant
                        ON tenant."Id" = subscription."TenantId"
                    WHERE tenant."IsDeleted" = FALSE
                        AND tenant."Name" <> {SystemTenantConstants.Name}
                )
                SELECT
                    module."Id" AS "ModuleId",
                    module."Name" AS "ModuleName",
                    EXTRACT(MONTH FROM months."MonthStart")::integer AS "Month",
                    EXTRACT(YEAR FROM months."MonthStart")::integer AS "Year",
                    COUNT(DISTINCT subscription."TenantId") FILTER (
                        WHERE subscription."IsDeleted" = FALSE
                            AND subscription."Status" IN ('Active', 'Trial')
                            AND subscription."StartDate" < months."MonthStart" + INTERVAL '1 month'
                            AND subscription."EndDate" >= months."MonthStart"
                    )::integer AS "ActiveCompanies",
                    COUNT(DISTINCT subscription."TenantId") FILTER (
                        WHERE subscription."IsDeleted" = TRUE
                            AND subscription."Status" = 'Suspended'
                            AND subscription."UpdatedAt" >= months."MonthStart"
                            AND subscription."UpdatedAt" < months."MonthStart" + INTERVAL '1 month'
                    )::integer AS "Cancellations",
                    COUNT(DISTINCT subscription."TenantId") FILTER (
                        WHERE subscription."IsDeleted" = FALSE
                            AND subscription."EndDate" >= months."MonthStart"
                            AND subscription."EndDate" < months."MonthStart" + INTERVAL '1 month'
                    )::integer AS "Expirations"
                FROM "Modules" AS module
                CROSS JOIN months
                LEFT JOIN customer_subscriptions AS subscription
                    ON subscription."ModuleId" = module."Id"
                GROUP BY module."Id", module."Name", months."MonthStart"
                ORDER BY module."Id", months."MonthStart"
                """)
            .ToListAsync(cancellationToken);

        return new SystemModuleTrendDto
        {
            From = fromUtc.ToString("yyyy-MM"),
            To = toExclusiveUtc.AddMonths(-1).ToString("yyyy-MM"),
            Series = rows
                .GroupBy(row => new { row.ModuleId, row.ModuleName })
                .Select(group => new SystemModuleTrendSeriesDto
                {
                    ModuleId = group.Key.ModuleId,
                    ModuleName = group.Key.ModuleName,
                    Points = group.Select(row => new SystemModuleTrendPointDto
                    {
                        Month = row.Month,
                        Year = row.Year,
                        ActiveCompanies = row.ActiveCompanies,
                        Cancellations = row.Cancellations,
                        Expirations = row.Expirations
                    }).ToList()
                })
                .ToList()
        };
    }

    private IQueryable<Guid> CustomerTenantIds()
        => _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Name != SystemTenantConstants.Name)
            .Select(t => t.Id);

    private sealed class ModuleTrendRow
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int ActiveCompanies { get; set; }
        public int Cancellations { get; set; }
        public int Expirations { get; set; }
    }
}
