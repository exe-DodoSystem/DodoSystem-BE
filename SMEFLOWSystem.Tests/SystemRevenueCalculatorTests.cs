using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Interfaces.IRepositories;

namespace SMEFLOWSystem.Tests;

public sealed class SystemRevenueCalculatorTests
{
    [Fact]
    public void CalculateFinalAmount_UsesStoredFinalOrDiscountFallback()
    {
        Assert.Equal(75m, AnalyticsMetricCalculator.CalculateFinalAmount(100m, 25m, null));
        Assert.Equal(80m, AnalyticsMetricCalculator.CalculateFinalAmount(100m, 25m, 80m));
        Assert.Equal(100m, AnalyticsMetricCalculator.CalculateFinalAmount(100m, null, null));
    }

    [Fact]
    public void SumCollected_UsesOnlySuccessfulPaymentsWithProcessedAt()
    {
        var processedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var payments = new[]
        {
            Payment("Success", processedAt, 100m),
            Payment("succeeded", processedAt, 200m),
            Payment("Settled", processedAt, 300m),
            Payment("Paid", processedAt, 400m),
            Payment("Failed", processedAt, 500m),
            Payment("Pending", processedAt, 600m),
            Payment("Success", null, 700m)
        };

        var collected = AnalyticsMetricCalculator.SumCollected(payments);

        Assert.Equal(1000m, collected);
    }

    [Fact]
    public void CalculateEstimatedMrr_UsesOnlyActiveSubscriptionsAtSnapshot()
    {
        var snapshot = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var subscriptions = new[]
        {
            Subscription("Active", snapshot.AddMonths(-1), snapshot.AddMonths(1), 100m),
            Subscription("active", snapshot, snapshot.AddDays(1), 200m),
            Subscription("Trial", snapshot.AddMonths(-1), snapshot.AddMonths(1), 300m),
            Subscription("Active", snapshot.AddMonths(-2), snapshot, 400m),
            Subscription("Active", snapshot.AddDays(1), snapshot.AddMonths(1), 500m)
        };

        var mrr = AnalyticsMetricCalculator.CalculateEstimatedMrr(subscriptions, snapshot);

        Assert.Equal(300m, mrr);
    }

    [Theory]
    [InlineData(SystemAnalyticsGranularity.Day, 3)]
    [InlineData(SystemAnalyticsGranularity.Week, 1)]
    [InlineData(SystemAnalyticsGranularity.Month, 2)]
    public void RevenueBucketBuilder_CreatesCompleteBuckets(
        string granularity,
        int expectedCount)
    {
        var from = new DateOnly(2026, 6, 30);
        var to = new DateOnly(2026, 7, 2);

        var buckets = RevenueBucketBuilder.Build(from, to, granularity);

        Assert.Equal(expectedCount, buckets.Count);
        Assert.Equal(from, buckets[0].FromInclusive);
        Assert.Equal(to.AddDays(1), buckets[^1].ToExclusive);
    }

    [Fact]
    public void RevenueBucketBuilder_StartsWeekOnMonday()
    {
        var buckets = RevenueBucketBuilder.Build(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 10),
            SystemAnalyticsGranularity.Week);

        Assert.All(buckets, bucket => Assert.Equal(DayOfWeek.Monday, bucket.BucketStart.DayOfWeek));
        Assert.Equal(new DateOnly(2026, 6, 29), buckets[0].BucketStart);
    }

    [Fact]
    public void AverageNonNegative_ExcludesInvalidNegativeValues()
    {
        Assert.Equal(2m, AnalyticsMetricCalculator.AverageNonNegative([-1m, 1m, 3m]));
        Assert.Null(AnalyticsMetricCalculator.AverageNonNegative([-2m, -1m]));
    }

    private static CollectedPaymentRow Payment(
        string status,
        DateTime? processedAt,
        decimal amount)
    {
        return new CollectedPaymentRow
        {
            Status = status,
            ProcessedAt = processedAt,
            Amount = amount
        };
    }

    private static ActiveSubscriptionPriceRow Subscription(
        string status,
        DateTime startDate,
        DateTime endDate,
        decimal price)
    {
        return new ActiveSubscriptionPriceRow
        {
            Status = status,
            StartDate = startDate,
            EndDate = endDate,
            MonthlyPrice = price
        };
    }
}
