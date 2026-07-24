using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.Helpers.System;

public sealed record RevenueBucket(
    DateOnly BucketStart,
    DateOnly FromInclusive,
    DateOnly ToExclusive);

public static class RevenueBucketBuilder
{
    public static IReadOnlyList<RevenueBucket> Build(
        DateOnly from,
        DateOnly to,
        string granularity)
    {
        if (from > to)
        {
            throw new ArgumentException("From must be before or equal to To.", nameof(from));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(granularity);
        var normalizedGranularity = granularity.Trim().ToLowerInvariant();
        var periodEndExclusive = to.AddDays(1);
        var bucketStart = GetBucketStart(from, normalizedGranularity);
        var buckets = new List<RevenueBucket>();

        while (bucketStart < periodEndExclusive)
        {
            var nextBucketStart = GetNextBucketStart(bucketStart, normalizedGranularity);
            buckets.Add(new RevenueBucket(
                bucketStart,
                bucketStart < from ? from : bucketStart,
                nextBucketStart > periodEndExclusive ? periodEndExclusive : nextBucketStart));
            bucketStart = nextBucketStart;
        }

        return buckets;
    }

    public static DateOnly GetBucketStart(DateOnly date, string granularity)
    {
        return granularity switch
        {
            SystemAnalyticsGranularity.Day => date,
            SystemAnalyticsGranularity.Week => date.AddDays(-DaysSinceMonday(date.DayOfWeek)),
            SystemAnalyticsGranularity.Month => new DateOnly(date.Year, date.Month, 1),
            _ => throw new ArgumentException(
                "Granularity must be 'day', 'week', or 'month'.",
                nameof(granularity))
        };
    }

    private static DateOnly GetNextBucketStart(DateOnly bucketStart, string granularity)
    {
        return granularity switch
        {
            SystemAnalyticsGranularity.Day => bucketStart.AddDays(1),
            SystemAnalyticsGranularity.Week => bucketStart.AddDays(7),
            SystemAnalyticsGranularity.Month => bucketStart.AddMonths(1),
            _ => throw new ArgumentException(
                "Granularity must be 'day', 'week', or 'month'.",
                nameof(granularity))
        };
    }

    private static int DaysSinceMonday(DayOfWeek dayOfWeek)
    {
        return ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    }
}
