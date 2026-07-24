using System;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Options;

namespace SMEFLOWSystem.Application.Helpers.System;

public static class AnalyticsPeriodResolver
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { /* Windows fallback */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { /* Linux fallback */ }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
    }

    public static TimeZoneInfo GetTimeZone(string timezoneName)
    {
        if (string.Equals(timezoneName, SystemAnalyticsOptions.SupportedTimezone, StringComparison.OrdinalIgnoreCase))
        {
            return VietnamTimeZone;
        }

        throw new TimeZoneNotFoundException(
            $"Timezone '{timezoneName}' is not supported. Only '{SystemAnalyticsOptions.SupportedTimezone}' is allowed.");
    }

    public static ResolvedPeriod Resolve(
        SystemAnalyticsPeriodQueryDto query,
        SystemAnalyticsOptions options,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        if (options.DefaultRangeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DefaultRangeDays),
                "DefaultRangeDays must be greater than zero.");
        }

        if (options.MaxRangeMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxRangeMonths),
                "MaxRangeMonths must be greater than zero.");
        }

        var tz = GetTimeZone(query.Timezone);
        var effectiveUtcNow = utcNow ?? DateTime.UtcNow;
        if (effectiveUtcNow.Kind != DateTimeKind.Utc)
        {
            effectiveUtcNow = effectiveUtcNow.ToUniversalTime();
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(effectiveUtcNow, tz);
        var todayLocal = DateOnly.FromDateTime(localNow);

        var from = query.From ?? todayLocal.AddDays(1 - options.DefaultRangeDays);
        var to = query.To ?? todayLocal;

        if (from > to)
        {
            throw new AnalyticsPeriodValidationException(
                query.From.HasValue ? nameof(query.From) : nameof(query.To),
                "Ngày bắt đầu (From) phải nhỏ hơn hoặc bằng ngày kết thúc (To).");
        }

        if (to > from.AddMonths(options.MaxRangeMonths))
        {
            throw new AnalyticsPeriodValidationException(
                nameof(query.To),
                $"Khoảng thời gian truy vấn không được vượt quá {options.MaxRangeMonths} tháng.");
        }

        // Convert boundary local dates to UTC
        var fromDateTimeLocal = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(fromDateTimeLocal, tz);

        var toDateTimeLocal = new DateTime(to.Year, to.Month, to.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
        var endExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(toDateTimeLocal, tz);

        // Previous Period calculations
        DateOnly? previousFrom = null;
        DateOnly? previousTo = null;
        DateTime? previousStartUtc = null;
        DateTime? previousEndExclusiveUtc = null;

        if (string.Equals(query.Compare, SystemAnalyticsCompare.PreviousPeriod, StringComparison.OrdinalIgnoreCase))
        {
            int days = to.DayNumber - from.DayNumber + 1;
            previousFrom = from.AddDays(-days);
            previousTo = to.AddDays(-days);

            var prevFromLocal = new DateTime(previousFrom.Value.Year, previousFrom.Value.Month, previousFrom.Value.Day, 0, 0, 0, DateTimeKind.Unspecified);
            previousStartUtc = TimeZoneInfo.ConvertTimeToUtc(prevFromLocal, tz);

            var prevToLocal = new DateTime(previousTo.Value.Year, previousTo.Value.Month, previousTo.Value.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
            previousEndExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(prevToLocal, tz);
        }
        else if (string.Equals(query.Compare, SystemAnalyticsCompare.PreviousYear, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                previousFrom = from.AddYears(-1);
                previousTo = to.AddYears(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Leap year or bounds fallback
                previousFrom = from.AddDays(-365);
                previousTo = to.AddDays(-365);
            }

            var prevFromLocal = new DateTime(previousFrom.Value.Year, previousFrom.Value.Month, previousFrom.Value.Day, 0, 0, 0, DateTimeKind.Unspecified);
            previousStartUtc = TimeZoneInfo.ConvertTimeToUtc(prevFromLocal, tz);

            var prevToLocal = new DateTime(previousTo.Value.Year, previousTo.Value.Month, previousTo.Value.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
            previousEndExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(prevToLocal, tz);
        }

        return new ResolvedPeriod
        {
            From = from,
            To = to,
            StartUtc = startUtc,
            EndExclusiveUtc = endExclusiveUtc,
            PreviousFrom = previousFrom,
            PreviousTo = previousTo,
            PreviousStartUtc = previousStartUtc,
            PreviousEndExclusiveUtc = previousEndExclusiveUtc
        };
    }

    /// <summary>
    /// Auto-select granularity based on date range:
    /// &lt;= 31 days → day, 32-180 days → week, &gt; 180 days → month.
    /// </summary>
    public static string ResolveGranularity(string? requested, DateOnly from, DateOnly to)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var normalized = requested.Trim().ToLowerInvariant();
            if (normalized == SystemAnalyticsGranularity.Day ||
                normalized == SystemAnalyticsGranularity.Week ||
                normalized == SystemAnalyticsGranularity.Month)
                return normalized;

            throw new ArgumentException(
                "Granularity must be 'day', 'week', or 'month'.",
                nameof(requested));
        }

        if (from > to)
        {
            throw new ArgumentException("From must be before or equal to To.", nameof(from));
        }

        int days = to.DayNumber - from.DayNumber + 1;
        return days switch
        {
            <= 31  => SystemAnalyticsGranularity.Day,
            <= 180 => SystemAnalyticsGranularity.Week,
            _      => SystemAnalyticsGranularity.Month
        };
    }

    /// <summary>Build <see cref="SystemAnalyticsMetaDto"/> from a resolved period.</summary>
    public static SystemAnalyticsMetaDto BuildMeta(
        ResolvedPeriod period,
        SystemAnalyticsPeriodQueryDto query,
        string mrrStatus = SystemAnalyticsMrrStatus.Unavailable)
    {
        if (mrrStatus != SystemAnalyticsMrrStatus.Estimated &&
            mrrStatus != SystemAnalyticsMrrStatus.Unavailable)
        {
            throw new ArgumentException("Unsupported MRR status.", nameof(mrrStatus));
        }

        return new SystemAnalyticsMetaDto
        {
            From = period.From.ToString("yyyy-MM-dd"),
            To = period.To.ToString("yyyy-MM-dd"),
            PreviousFrom = period.PreviousFrom?.ToString("yyyy-MM-dd"),
            PreviousTo = period.PreviousTo?.ToString("yyyy-MM-dd"),
            Timezone = SystemAnalyticsOptions.SupportedTimezone,
            Currency = SystemAnalyticsOptions.SupportedCurrency,
            GeneratedAt = DateTime.UtcNow,
            DataThrough = null,
            Freshness = "Live",
            ExcludesInternalTenant = true,
            ExcludesTestTenants = false,
            MrrStatus = mrrStatus
        };
    }
}

public sealed class AnalyticsPeriodValidationException : Exception
{
    public AnalyticsPeriodValidationException(string propertyName, string message)
        : base(message)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}

public sealed class ResolvedPeriod
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndExclusiveUtc { get; set; }
    public DateOnly? PreviousFrom { get; set; }
    public DateOnly? PreviousTo { get; set; }
    public DateTime? PreviousStartUtc { get; set; }
    public DateTime? PreviousEndExclusiveUtc { get; set; }
}
