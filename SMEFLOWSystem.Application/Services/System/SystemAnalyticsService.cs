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

    public async Task<SystemActionCenterResponseDto> GetActionCenterAsync(
        CancellationToken ct = default)
    {
        if (_options.OrderOverdueGraceHours < 0)
        {
            throw new InvalidOperationException(
                "SystemAnalytics:OrderOverdueGraceHours cannot be negative.");
        }
        if (_options.ActionCenterMaxItems <= 0)
        {
            throw new InvalidOperationException(
                "SystemAnalytics:ActionCenterMaxItems must be greater than zero.");
        }

        var nowUtc = DateTime.UtcNow;
        var candidates = await _repository.GetActionCenterCandidatesAsync(
            nowUtc,
            _options.OrderOverdueGraceHours,
            ct);
        var mappedItems = new List<SystemActionCenterItemDto>(candidates.Count);
        var unknownTypeCount = 0;

        foreach (var candidate in candidates)
        {
            if (TryCreateActionCenterItem(
                    candidate,
                    _options.OrderOverdueGraceHours,
                    out var item))
            {
                mappedItems.Add(item);
            }
            else
            {
                unknownTypeCount++;
            }
        }

        if (unknownTypeCount > 0)
        {
            _logger.LogWarning(
                "Excluded {UnknownActionCenterTypeCount} action-center candidates with unrecognized types.",
                unknownTypeCount);
        }

        var uniqueItems = mappedItems
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.OccurredAt).First())
            .ToList();
        var counts = new SystemActionCenterCountsDto
        {
            Critical = uniqueItems.Count(item =>
                item.Severity == SystemActionCenterSeverity.Critical),
            Warning = uniqueItems.Count(item =>
                item.Severity == SystemActionCenterSeverity.Warning),
            Info = uniqueItems.Count(item =>
                item.Severity == SystemActionCenterSeverity.Info)
        };
        var items = uniqueItems
            .OrderBy(item => GetSeverityRank(item.Severity))
            .ThenByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(_options.ActionCenterMaxItems)
            .ToList();

        var timeZone = AnalyticsPeriodResolver.GetTimeZone(
            _options.BusinessTimezone);
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone));
        var metaQuery = new SystemAnalyticsPeriodQueryDto
        {
            From = localToday,
            To = localToday,
            Timezone = SystemAnalyticsOptions.SupportedTimezone,
            Currency = SystemAnalyticsOptions.SupportedCurrency,
            Compare = SystemAnalyticsCompare.None
        };
        var metaPeriod = AnalyticsPeriodResolver.Resolve(
            metaQuery,
            _options,
            nowUtc);
        var meta = AnalyticsPeriodResolver.BuildMeta(metaPeriod, metaQuery);
        meta.GeneratedAt = nowUtc;
        meta.DataThrough = nowUtc;
        meta.Warnings =
        [
            SystemAnalyticsWarningCodes.OrderOverdueUsesConfiguredGracePeriod,
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable
        ];

        return new SystemActionCenterResponseDto
        {
            Counts = counts,
            Items = items,
            Meta = meta
        };
    }

    public async Task<SystemRevenueForecastResponseDto> GetRevenueForecastAsync(
        SystemRevenueForecastQueryDto query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_options.ForecastMinimumMonths < 2)
        {
            throw new InvalidOperationException(
                "SystemAnalytics:ForecastMinimumMonths must be at least two.");
        }
        if (_options.ForecastMaximumPeriods is < 1 or > 6)
        {
            throw new InvalidOperationException(
                "SystemAnalytics:ForecastMaximumPeriods must be between one and six.");
        }

        if (!string.IsNullOrWhiteSpace(query.Granularity)
            && !string.Equals(
                query.Granularity,
                SystemAnalyticsGranularity.Month,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.Granularity),
                "Granularity must be 'month'.");
        }

        var forecastPeriods = query.ForecastPeriods ?? 3;
        if (forecastPeriods < 1
            || forecastPeriods > _options.ForecastMaximumPeriods)
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.ForecastPeriods),
                $"ForecastPeriods must be between 1 and {_options.ForecastMaximumPeriods}.");
        }

        var nowUtc = DateTime.UtcNow;
        var period = AnalyticsPeriodResolver.Resolve(query, _options, nowUtc);
        var trainingMonths = GetCompleteMonths(period.From, period.To);
        if (trainingMonths.Count < _options.ForecastMinimumMonths)
        {
            throw new InsufficientForecastHistoryException(
                _options.ForecastMinimumMonths,
                trainingMonths.Count);
        }

        if (query.ModuleId.HasValue
            && !await _repository.ModuleExistsAsync(query.ModuleId.Value, ct))
        {
            throw new SystemAnalyticsQueryValidationException(
                nameof(query.ModuleId),
                $"Module with ID '{query.ModuleId.Value}' does not exist.");
        }

        var tenantSegment = query.TenantSegment.Trim().ToLowerInvariant();
        var timeZone = AnalyticsPeriodResolver.GetTimeZone(query.Timezone);
        var trainingStartUtc = ToUtcBoundary(trainingMonths[0], timeZone);
        var trainingEndExclusiveUtc = ToUtcBoundary(
            trainingMonths[^1].AddMonths(1),
            timeZone);
        var rows = await _repository.GetMonthlyCollectedRevenueAsync(
            trainingStartUtc,
            trainingEndExclusiveUtc,
            query.ModuleId,
            tenantSegment,
            ct);
        var valuesByMonth = rows
            .GroupBy(row => new DateOnly(row.Year, row.Month, 1))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.CollectedRevenue));
        var availableMonths = trainingMonths.Count(valuesByMonth.ContainsKey);
        if (availableMonths != trainingMonths.Count)
        {
            throw new InsufficientForecastHistoryException(
                trainingMonths.Count,
                availableMonths);
        }

        var trainingPoints = trainingMonths
            .Select(month => new RevenueForecastInputPoint(
                month,
                valuesByMonth[month]))
            .ToList();
        var calculation = RevenueForecastCalculator.Calculate(
            trainingPoints,
            forecastPeriods);
        var trainingTo = trainingMonths[^1].AddMonths(1).AddDays(-1);
        var forecastTo = calculation.Points[^1]
            .BucketStart
            .AddMonths(1)
            .AddDays(-1);
        var meta = AnalyticsPeriodResolver.BuildMeta(period, query);
        meta.From = trainingMonths[0].ToString("yyyy-MM-dd");
        meta.To = forecastTo.ToString("yyyy-MM-dd");
        meta.PreviousFrom = null;
        meta.PreviousTo = null;
        meta.GeneratedAt = nowUtc;
        meta.DataThrough = trainingEndExclusiveUtc.AddTicks(-1);
        meta.Warnings =
        [
            SystemAnalyticsWarningCodes.ForecastExcludesRefunds,
            SystemAnalyticsWarningCodes.ForecastBasedOnAvailablePaymentHistory,
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable
        ];

        return new SystemRevenueForecastResponseDto
        {
            Method = "LinearTrend",
            TrainingFrom = trainingMonths[0].ToString("yyyy-MM-dd"),
            TrainingTo = trainingTo.ToString("yyyy-MM-dd"),
            Currency = SystemAnalyticsOptions.SupportedCurrency,
            Granularity = SystemAnalyticsGranularity.Month,
            ActualPoints = trainingPoints
                .Select(point => new SystemRevenueForecastActualPointDto
                {
                    BucketStart = point.BucketStart.ToString("yyyy-MM-dd"),
                    Value = point.Value
                })
                .ToList(),
            ForecastPoints = calculation.Points
                .Select(point => new SystemRevenueForecastPointDto
                {
                    BucketStart = point.BucketStart.ToString("yyyy-MM-dd"),
                    Value = point.Value,
                    LowerBound = point.LowerBound,
                    UpperBound = point.UpperBound
                })
                .ToList(),
            Meta = meta
        };
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

    private static IReadOnlyList<DateOnly> GetCompleteMonths(
        DateOnly from,
        DateOnly to)
    {
        var firstMonth = new DateOnly(from.Year, from.Month, 1);
        if (from.Day != 1)
        {
            firstMonth = firstMonth.AddMonths(1);
        }

        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        if (to.Day != DateTime.DaysInMonth(to.Year, to.Month))
        {
            lastMonth = lastMonth.AddMonths(-1);
        }

        if (firstMonth > lastMonth)
        {
            return [];
        }

        var months = new List<DateOnly>();
        for (var month = firstMonth;
             month <= lastMonth;
             month = month.AddMonths(1))
        {
            months.Add(month);
        }
        return months;
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

    private static bool TryCreateActionCenterItem(
        ActionCenterCandidateRow candidate,
        int overdueGraceHours,
        out SystemActionCenterItemDto item)
    {
        var type = NormalizeActionCenterType(candidate.Type);
        if (type == null)
        {
            item = null!;
            return false;
        }

        var entityName = SanitizeActionCenterLabel(
            candidate.EntityName,
            candidate.EntityId.ToString("D"));
        var tenantName = SanitizeActionCenterLabel(
            candidate.TenantName,
            candidate.TenantId.ToString("D"));
        var (severity, title, description) = type switch
        {
            SystemActionCenterItemType.PaymentFailed => (
                SystemActionCenterSeverity.Critical,
                "Thanh toán thất bại",
                $"Thanh toán cho hóa đơn {entityName} của {tenantName} đã thất bại."),
            SystemActionCenterItemType.OrderOverdue => (
                SystemActionCenterSeverity.Critical,
                "Hóa đơn quá hạn thanh toán",
                $"Hóa đơn {entityName} của {tenantName} đã quá hạn thanh toán {overdueGraceHours} giờ."),
            SystemActionCenterItemType.SubscriptionExpiring => (
                SystemActionCenterSeverity.Warning,
                "Gói đăng ký sắp hết hạn",
                $"Gói {entityName} của {tenantName} sẽ hết hạn trong vòng 7 ngày."),
            SystemActionCenterItemType.TrialEnding => (
                SystemActionCenterSeverity.Warning,
                "Gói dùng thử sắp kết thúc",
                $"Gói dùng thử {entityName} của {tenantName} sẽ kết thúc trong vòng 7 ngày."),
            SystemActionCenterItemType.TenantSuspended => (
                SystemActionCenterSeverity.Info,
                "Tenant đang bị tạm ngưng",
                $"Tenant {tenantName} đang ở trạng thái tạm ngưng."),
            _ => throw new InvalidOperationException(
                $"Unsupported action-center type '{type}'.")
        };

        item = new SystemActionCenterItemDto
        {
            Id = $"{type}_{candidate.EntityId:D}",
            Type = type,
            Severity = severity,
            Title = title,
            Description = description,
            OccurredAt = EnsureUtc(candidate.OccurredAt),
            EntityId = candidate.EntityId,
            TargetPath = BuildActionCenterTargetPath(
                type,
                candidate.EntityId,
                candidate.TenantId)
        };
        return true;
    }

    private static string? NormalizeActionCenterType(string? type)
    {
        var knownTypes = new[]
        {
            SystemActionCenterItemType.PaymentFailed,
            SystemActionCenterItemType.OrderOverdue,
            SystemActionCenterItemType.SubscriptionExpiring,
            SystemActionCenterItemType.TrialEnding,
            SystemActionCenterItemType.TenantSuspended
        };
        return knownTypes.FirstOrDefault(known =>
            string.Equals(known, type, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildActionCenterTargetPath(
        string type,
        Guid entityId,
        Guid tenantId)
    {
        return type switch
        {
            SystemActionCenterItemType.PaymentFailed =>
                "/system-admin/payment-transactions",
            SystemActionCenterItemType.OrderOverdue =>
                $"/system-admin/billing-orders/{entityId:D}",
            SystemActionCenterItemType.SubscriptionExpiring or
                SystemActionCenterItemType.TrialEnding =>
                $"/system-admin/subscriptions?tenantId={tenantId:D}",
            SystemActionCenterItemType.TenantSuspended =>
                $"/system-admin/tenants/{tenantId:D}",
            _ => throw new InvalidOperationException(
                $"Unsupported action-center type '{type}'.")
        };
    }

    private static string SanitizeActionCenterLabel(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var sanitized = new string(value
            .Where(character => !char.IsControl(character))
            .Take(200)
            .ToArray())
            .Trim();
        return sanitized.Length == 0 ? fallback : sanitized;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static int GetSeverityRank(string severity)
    {
        return severity switch
        {
            SystemActionCenterSeverity.Critical => 0,
            SystemActionCenterSeverity.Warning => 1,
            SystemActionCenterSeverity.Info => 2,
            _ => int.MaxValue
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
