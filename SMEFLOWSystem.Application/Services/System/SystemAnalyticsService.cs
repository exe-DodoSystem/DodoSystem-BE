using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.Application.Options;

namespace SMEFLOWSystem.Application.Services.System;

public sealed class SystemAnalyticsService : ISystemAnalyticsService
{
    private readonly ISystemAnalyticsReadRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly SystemAnalyticsOptions _options;
    private readonly ILogger<SystemAnalyticsService> _logger;

    public SystemAnalyticsService(
        ISystemAnalyticsReadRepository repository,
        IMemoryCache cache,
        IOptions<SystemAnalyticsOptions> options,
        ILogger<SystemAnalyticsService> logger)
    {
        _repository = repository;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SystemRevenueSeriesResponseDto> GetRevenueSeriesAsync(
        SystemRevenueSeriesQueryDto query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = AnalyticsPeriodResolver.Resolve(query, _options);
        var granularity = AnalyticsPeriodResolver.ResolveGranularity(
            query.Granularity,
            period.From,
            period.To);
        var normalizedSegment = query.TenantSegment.Trim().ToLowerInvariant();

        var cacheKey = BuildCacheKey(
            period,
            query,
            normalizedSegment,
            granularity);

        return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    if (query.ModuleId.HasValue
                        && !await _repository.ModuleExistsAsync(query.ModuleId.Value, ct))
                    {
                        throw new SystemAnalyticsQueryValidationException(
                            nameof(query.ModuleId),
                            $"Module with ID '{query.ModuleId.Value}' does not exist.");
                    }

                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(
                        _options.CacheSeconds);
                    return await BuildRevenueSeriesAsync(
                        period,
                        query,
                        normalizedSegment,
                        granularity,
                        ct);
                })
            ?? throw new InvalidOperationException("Revenue series cache factory returned no value.");
    }

    public async Task<SystemRevenueBreakdownResponseDto> GetRevenueBreakdownAsync(
        SystemRevenueBreakdownQueryDto query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = AnalyticsPeriodResolver.Resolve(query, _options);
        if (query.ModuleId.HasValue
            && !await _repository.ModuleExistsAsync(query.ModuleId.Value, ct))
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.ModuleId),
                $"Module with ID '{query.ModuleId.Value}' does not exist.");
        }

        var dimension = query.Dimension.Trim().ToLowerInvariant();
        if (dimension is not (
                SystemAnalyticsDimension.Module
                or SystemAnalyticsDimension.Tenant
                or SystemAnalyticsDimension.Gateway))
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.Dimension),
                "Dimension must be 'module', 'tenant', or 'gateway'.");
        }

        var tenantSegment = query.TenantSegment.Trim().ToLowerInvariant();
        var payments = await _repository.GetRevenuePaymentsAsync(
            period.StartUtc,
            period.EndExclusiveUtc,
            query.ModuleId,
            tenantSegment,
            ct);
        var successfulPayments = SelectSuccessfulPayments(payments, out var paymentWarnings);
        var totalCollectedRevenue = successfulPayments.Sum(payment => payment.Amount);

        List<BreakdownAmount> amounts;
        var unallocatedAmount = 0m;
        var warnings = new HashSet<string>(paymentWarnings, StringComparer.Ordinal)
        {
            SystemAnalyticsWarningCodes.RefundDataUnavailable,
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable
        };

        switch (dimension)
        {
            case SystemAnalyticsDimension.Tenant:
                amounts = successfulPayments
                    .GroupBy(payment => new { payment.TenantId, payment.TenantName })
                    .Select(group => new BreakdownAmount(
                        group.Key.TenantId.ToString(),
                        group.Key.TenantName,
                        group.Sum(payment => payment.Amount)))
                    .ToList();
                break;

            case SystemAnalyticsDimension.Gateway:
                amounts = successfulPayments
                    .GroupBy(
                        payment => payment.Gateway.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => new BreakdownAmount(
                        group.Key.ToLowerInvariant(),
                        group.Key,
                        group.Sum(payment => payment.Amount)))
                    .ToList();
                break;

            default:
                (amounts, unallocatedAmount) = await AllocatePaymentsToModulesAsync(
                    successfulPayments,
                    ct);
                if (unallocatedAmount != 0m)
                {
                    warnings.Add(
                        SystemAnalyticsWarningCodes.OrderModuleAllocationUnavailable);
                }
                break;
        }

        var orderedAmounts = amounts
            .Where(item => item.Amount != 0m)
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        var limit = query.Limit ?? 10;
        var visibleAmounts = orderedAmounts.Take(limit).ToList();
        var otherAmount = orderedAmounts.Skip(limit).Sum(item => item.Amount)
            + unallocatedAmount;

        var meta = AnalyticsPeriodResolver.BuildMeta(period, query);
        meta.DataThrough = successfulPayments.Count == 0
            ? null
            : successfulPayments.Max(payment => payment.ProcessedAt);
        meta.Warnings = warnings.OrderBy(code => code, StringComparer.Ordinal).ToList();

        return new SystemRevenueBreakdownResponseDto
        {
            TotalCollectedRevenue = totalCollectedRevenue,
            Items = visibleAmounts
                .Select(item => ToBreakdownItem(item, totalCollectedRevenue))
                .ToList(),
            Other = otherAmount == 0m
                ? null
                : ToBreakdownItem(
                    new BreakdownAmount("OTHER", "Khác", otherAmount),
                    totalCollectedRevenue),
            Meta = meta
        };
    }

    public Task<SystemActionCenterResponseDto> GetActionCenterAsync(
        CancellationToken ct = default)
    {
        throw new NotSupportedException("Action center is scheduled for Phase 5.");
    }

    public Task<SystemRevenueForecastResponseDto> GetRevenueForecastAsync(
        SystemRevenueForecastQueryDto query,
        CancellationToken ct = default)
    {
        throw new NotSupportedException("Revenue forecast is scheduled for Phase 8.");
    }

    private async Task<SystemRevenueSeriesResponseDto> BuildRevenueSeriesAsync(
        ResolvedPeriod period,
        SystemRevenueSeriesQueryDto query,
        string tenantSegment,
        string granularity,
        CancellationToken ct)
    {
        var current = await LoadPeriodAsync(
            period.From,
            period.To,
            period.StartUtc,
            period.EndExclusiveUtc,
            query.ModuleId,
            tenantSegment,
            granularity,
            query.Timezone,
            ct);

        PeriodSeriesData? previous = null;
        if (period.PreviousFrom.HasValue
            && period.PreviousTo.HasValue
            && period.PreviousStartUtc.HasValue
            && period.PreviousEndExclusiveUtc.HasValue)
        {
            previous = await LoadPeriodAsync(
                period.PreviousFrom.Value,
                period.PreviousTo.Value,
                period.PreviousStartUtc.Value,
                period.PreviousEndExclusiveUtc.Value,
                query.ModuleId,
                tenantSegment,
                granularity,
                query.Timezone,
                ct);
        }

        var warnings = new HashSet<string>(StringComparer.Ordinal)
        {
            SystemAnalyticsWarningCodes.RefundDataUnavailable,
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable,
            SystemAnalyticsWarningCodes.MrrUsesCurrentCatalogPrice
        };
        warnings.UnionWith(current.Warnings);
        if (previous != null)
        {
            warnings.UnionWith(previous.Warnings);
        }

        var meta = AnalyticsPeriodResolver.BuildMeta(
            period,
            query,
            SystemAnalyticsMrrStatus.Estimated);
        meta.DataThrough = MaxTimestamp(current.DataThrough, previous?.DataThrough);
        meta.Warnings = warnings.OrderBy(code => code, StringComparer.Ordinal).ToList();

        return new SystemRevenueSeriesResponseDto
        {
            Points = current.Points,
            PreviousPoints = previous?.Points,
            Meta = meta
        };
    }

    private async Task<PeriodSeriesData> LoadPeriodAsync(
        DateOnly from,
        DateOnly to,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? moduleId,
        string tenantSegment,
        string granularity,
        string timezone,
        CancellationToken ct)
    {
        // DbContext is scoped and does not allow concurrent operations, so these
        // independent projections intentionally execute sequentially.
        var invoicedOrders = await _repository.GetInvoicedOrdersAsync(
            fromUtc,
            toExclusiveUtc,
            moduleId,
            tenantSegment,
            ct);
        var payments = await _repository.GetRevenuePaymentsAsync(
            fromUtc,
            toExclusiveUtc,
            moduleId,
            tenantSegment,
            ct);
        var outstandingOrders = await _repository.GetPendingOutstandingOrdersAsync(
            fromUtc,
            toExclusiveUtc,
            moduleId,
            tenantSegment,
            ct);
        var subscriptions = await _repository.GetActiveSubscriptionPricesAsync(
            fromUtc,
            toExclusiveUtc,
            moduleId,
            tenantSegment,
            ct);

        var timeZone = AnalyticsPeriodResolver.GetTimeZone(timezone);
        var buckets = RevenueBucketBuilder.Build(from, to, granularity);
        var values = buckets.ToDictionary(
            bucket => bucket.BucketStart,
            _ => new MutableRevenuePoint());
        DateTime? dataThrough = null;

        foreach (var order in invoicedOrders)
        {
            var localDate = ToLocalDate(order.BillingDate, timeZone);
            if (!IsInPeriod(localDate, from, to))
            {
                continue;
            }

            values[RevenueBucketBuilder.GetBucketStart(localDate, granularity)]
                .InvoicedRevenue += order.FinalAmount;
            dataThrough = MaxTimestamp(dataThrough, order.BillingDate);
        }

        var warnings = new HashSet<string>(StringComparer.Ordinal);
        var unknownStatusCount = 0;
        var missingProcessedAtCount = 0;
        foreach (var payment in payments)
        {
            if (PaymentStatusClassifier.IsSuccessful(payment.Status))
            {
                if (!payment.ProcessedAt.HasValue)
                {
                    missingProcessedAtCount++;
                    continue;
                }

                var localDate = ToLocalDate(payment.ProcessedAt.Value, timeZone);
                if (!IsInPeriod(localDate, from, to))
                {
                    continue;
                }

                values[RevenueBucketBuilder.GetBucketStart(localDate, granularity)]
                    .CollectedRevenue += payment.Amount;
                dataThrough = MaxTimestamp(dataThrough, payment.ProcessedAt);
                continue;
            }

            if (!PaymentStatusClassifier.IsFailed(payment.Status)
                && !string.Equals(
                    payment.Status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                unknownStatusCount++;
            }
        }

        if (missingProcessedAtCount > 0)
        {
            warnings.Add(SystemAnalyticsWarningCodes.PaymentWithoutProcessedAtExcluded);
        }
        if (unknownStatusCount > 0)
        {
            warnings.Add(SystemAnalyticsWarningCodes.PaymentStatusUnrecognized);
            _logger.LogWarning(
                "Excluded {UnknownPaymentStatusCount} payments with unrecognized statuses from System Analytics revenue.",
                unknownStatusCount);
        }

        foreach (var order in outstandingOrders)
        {
            var localDate = ToLocalDate(order.CreatedAt, timeZone);
            if (!IsInPeriod(localDate, from, to))
            {
                continue;
            }

            values[RevenueBucketBuilder.GetBucketStart(localDate, granularity)]
                .OutstandingCreated += order.FinalAmount;
            dataThrough = MaxTimestamp(dataThrough, order.CreatedAt);
        }

        foreach (var subscription in subscriptions)
        {
            dataThrough = MaxTimestamp(dataThrough, subscription.DataUpdatedAt);
        }

        var points = new List<SystemRevenueSeriesPointDto>(buckets.Count);
        foreach (var bucket in buckets)
        {
            var bucketEndUtc = ToUtcBoundary(bucket.ToExclusive, timeZone);
            var snapshotAtUtc = bucketEndUtc.AddTicks(-1);
            var value = values[bucket.BucketStart];
            points.Add(new SystemRevenueSeriesPointDto
            {
                BucketStart = bucket.BucketStart.ToString("yyyy-MM-dd"),
                InvoicedRevenue = value.InvoicedRevenue,
                CollectedRevenue = value.CollectedRevenue,
                RefundedAmount = null,
                OutstandingCreated = value.OutstandingCreated,
                MrrSnapshot = AnalyticsMetricCalculator.CalculateEstimatedMrr(
                    subscriptions,
                    snapshotAtUtc)
            });
        }

        return new PeriodSeriesData(points, warnings, dataThrough);
    }

    private static DateOnly ToLocalDate(DateTime utcTimestamp, TimeZoneInfo timeZone)
    {
        var utc = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }

    private static DateTime ToUtcBoundary(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var local = new DateTime(
            localDate.Year,
            localDate.Month,
            localDate.Day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private static bool IsInPeriod(DateOnly date, DateOnly from, DateOnly to)
    {
        return date >= from && date <= to;
    }

    private static DateTime? MaxTimestamp(DateTime? first, DateTime? second)
    {
        if (!first.HasValue)
        {
            return second;
        }
        if (!second.HasValue)
        {
            return first;
        }

        return first.Value >= second.Value ? first : second;
    }

    private List<CollectedPaymentRow> SelectSuccessfulPayments(
        IEnumerable<CollectedPaymentRow> payments,
        out IReadOnlySet<string> warnings)
    {
        var result = new List<CollectedPaymentRow>();
        var warningSet = new HashSet<string>(StringComparer.Ordinal);
        var unknownStatusCount = 0;

        foreach (var payment in payments)
        {
            if (PaymentStatusClassifier.IsSuccessful(payment.Status))
            {
                if (payment.ProcessedAt.HasValue)
                {
                    result.Add(payment);
                }
                else
                {
                    warningSet.Add(
                        SystemAnalyticsWarningCodes.PaymentWithoutProcessedAtExcluded);
                }

                continue;
            }

            if (!PaymentStatusClassifier.IsFailed(payment.Status)
                && !string.Equals(
                    payment.Status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                unknownStatusCount++;
            }
        }

        if (unknownStatusCount > 0)
        {
            warningSet.Add(SystemAnalyticsWarningCodes.PaymentStatusUnrecognized);
            _logger.LogWarning(
                "Excluded {UnknownPaymentStatusCount} payments with unrecognized statuses from System Analytics revenue breakdown.",
                unknownStatusCount);
        }

        warnings = warningSet;
        return result;
    }

    private async Task<(List<BreakdownAmount> Amounts, decimal UnallocatedAmount)>
        AllocatePaymentsToModulesAsync(
            IReadOnlyCollection<CollectedPaymentRow> successfulPayments,
            CancellationToken ct)
    {
        var orderIds = successfulPayments
            .Select(payment => payment.OrderId)
            .Distinct()
            .ToArray();
        var lines = await _repository.GetBillingOrderModuleAllocationsAsync(orderIds, ct);
        var linesByOrder = lines.ToLookup(line => line.OrderId);
        var amountsByModule = new Dictionary<string, BreakdownAmount>(
            StringComparer.Ordinal);
        var unallocatedAmount = 0m;

        foreach (var orderPayments in successfulPayments.GroupBy(payment => payment.OrderId))
        {
            var collectedAmount = orderPayments.Sum(payment => payment.Amount);
            var allocationInputs = linesByOrder[orderPayments.Key]
                .Where(line => line.LineTotal > 0m)
                .Select(line => new RevenueAllocationInput(
                    line.ModuleCode,
                    line.ModuleName,
                    line.LineTotal))
                .ToList();
            var allocation = RevenueAllocationCalculator.Allocate(
                collectedAmount,
                allocationInputs);

            unallocatedAmount += allocation.UnallocatedAmount;
            foreach (var item in allocation.Items)
            {
                if (amountsByModule.TryGetValue(item.Key, out var existing))
                {
                    amountsByModule[item.Key] = existing with
                    {
                        Amount = existing.Amount + item.Amount
                    };
                }
                else
                {
                    amountsByModule[item.Key] = new BreakdownAmount(
                        item.Key,
                        item.Name,
                        item.Amount);
                }
            }
        }

        return (amountsByModule.Values.ToList(), unallocatedAmount);
    }

    private static SystemRevenueBreakdownItemDto ToBreakdownItem(
        BreakdownAmount item,
        decimal totalCollectedRevenue)
    {
        return new SystemRevenueBreakdownItemDto
        {
            Id = item.Id,
            Name = item.Name,
            CollectedRevenue = item.Amount,
            PercentageOfTotal = AnalyticsMetricCalculator.CalculatePercentage(
                item.Amount,
                totalCollectedRevenue)
        };
    }

    private static string BuildCacheKey(
        ResolvedPeriod period,
        SystemRevenueSeriesQueryDto query,
        string tenantSegment,
        string granularity)
    {
        return string.Join(
            '|',
            "system-analytics:revenue-series:v1",
            period.From.ToString("yyyy-MM-dd"),
            period.To.ToString("yyyy-MM-dd"),
            SystemAnalyticsOptions.SupportedTimezone,
            SystemAnalyticsOptions.SupportedCurrency,
            query.Compare.Trim().ToLowerInvariant(),
            query.ModuleId?.ToString() ?? "all-modules",
            tenantSegment,
            granularity);
    }

    private sealed class MutableRevenuePoint
    {
        public decimal InvoicedRevenue { get; set; }
        public decimal CollectedRevenue { get; set; }
        public decimal OutstandingCreated { get; set; }
    }

    private sealed record PeriodSeriesData(
        List<SystemRevenueSeriesPointDto> Points,
        IReadOnlySet<string> Warnings,
        DateTime? DataThrough);

    private sealed record BreakdownAmount(
        string Id,
        string Name,
        decimal Amount);
}
