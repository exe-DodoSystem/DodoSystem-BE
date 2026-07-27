using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.Application.Options;

namespace SMEFLOWSystem.Application.Services.System;

public sealed class SystemTenantAnalyticsService : ISystemTenantAnalyticsService
{
    private readonly ISystemAnalyticsReadRepository _repository;
    private readonly SystemAnalyticsOptions _options;

    public SystemTenantAnalyticsService(
        ISystemAnalyticsReadRepository repository,
        IOptions<SystemAnalyticsOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<SystemTenantFinancialSummaryResponseDto>
        GetTenantFinancialSummaryAsync(
            Guid tenantId,
            SystemAnalyticsPeriodQueryDto query,
            CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var nowUtc = DateTime.UtcNow;
        var period = AnalyticsPeriodResolver.Resolve(query, _options, nowUtc);
        if (query.ModuleId.HasValue
            && !await _repository.ModuleExistsAsync(query.ModuleId.Value, ct))
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.ModuleId),
                $"Module with ID '{query.ModuleId.Value}' does not exist.");
        }

        var aggregate = await _repository.GetTenantFinancialAggregateAsync(
            tenantId,
            period.StartUtc,
            period.EndExclusiveUtc,
            query.ModuleId,
            ct);
        if (aggregate == null)
        {
            throw new KeyNotFoundException("Tenant not found.");
        }

        var subscriptionCounts =
            await _repository.GetTenantSubscriptionCountsAsync(
                tenantId,
                nowUtc,
                query.ModuleId,
                ct);
        var hasNegativePaymentDelay = aggregate.PaymentDelayDaysList
            .Any(delay => delay < 0m);
        var averagePaymentDelay = AnalyticsMetricCalculator.AverageNonNegative(
            aggregate.PaymentDelayDaysList);
        var warnings = new HashSet<string>(StringComparer.Ordinal)
        {
            SystemAnalyticsWarningCodes.RefundDataUnavailable,
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable,
            SystemAnalyticsWarningCodes.MrrUsesCurrentCatalogPrice
        };
        if (hasNegativePaymentDelay)
        {
            warnings.Add(
                SystemAnalyticsWarningCodes.PaymentDelayDaysNegativeExcluded);
        }

        var meta = AnalyticsPeriodResolver.BuildMeta(
            period,
            query,
            SystemAnalyticsMrrStatus.Estimated);
        meta.GeneratedAt = nowUtc;
        meta.DataThrough = nowUtc;
        meta.Warnings = warnings.OrderBy(
            warning => warning,
            StringComparer.Ordinal).ToList();

        return new SystemTenantFinancialSummaryResponseDto
        {
            TenantId = aggregate.TenantId,
            TenantName = aggregate.TenantName,
            Status = aggregate.Status,
            CurrentMrr = subscriptionCounts.EstimatedMrr,
            LifetimeCollectedRevenue = aggregate.LifetimeCollectedRevenue,
            CollectedRevenueInPeriod = aggregate.CollectedRevenueInPeriod,
            OutstandingAmount = aggregate.OutstandingAmount,
            LastSuccessfulPaymentAt = NormalizeUtc(
                aggregate.LastSuccessfulPaymentAt),
            LastFailedPaymentAt = NormalizeUtc(aggregate.LastFailedPaymentAt),
            AveragePaymentDelayDays = averagePaymentDelay.HasValue
                ? (double)averagePaymentDelay.Value
                : null,
            Subscriptions = new SystemTenantSubscriptionSummaryDto
            {
                Active = subscriptionCounts.Active,
                Trial = subscriptionCounts.Trial,
                ExpiringIn30Days = subscriptionCounts.ExpiringIn30Days
            },
            Meta = meta
        };
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
