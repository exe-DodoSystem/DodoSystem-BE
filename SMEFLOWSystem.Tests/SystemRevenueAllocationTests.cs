using SMEFLOWSystem.Application.Helpers.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemRevenueAllocationTests
{
    [Fact]
    public void Allocate_ReconcilesRoundingRemainderExactly()
    {
        var result = RevenueAllocationCalculator.Allocate(
            100m,
            [
                new RevenueAllocationInput("A", "Module A", 1m),
                new RevenueAllocationInput("B", "Module B", 1m),
                new RevenueAllocationInput("C", "Module C", 1m)
            ]);

        Assert.Equal([33.33m, 33.33m, 33.34m], result.Items.Select(item => item.Amount));
        Assert.Equal(100m, result.Items.Sum(item => item.Amount));
        Assert.Equal(0m, result.UnallocatedAmount);
    }

    [Fact]
    public void Allocate_GroupsDuplicateModuleLinesBeforeAllocation()
    {
        var result = RevenueAllocationCalculator.Allocate(
            90m,
            [
                new RevenueAllocationInput("A", "Module A", 20m),
                new RevenueAllocationInput("A", "Module A", 10m),
                new RevenueAllocationInput("B", "Module B", 60m)
            ]);

        Assert.Collection(
            result.Items,
            first =>
            {
                Assert.Equal("A", first.Key);
                Assert.Equal(30m, first.Amount);
            },
            second =>
            {
                Assert.Equal("B", second.Key);
                Assert.Equal(60m, second.Amount);
            });
    }

    [Fact]
    public void Allocate_ReturnsUnallocatedAmountWhenLinesHaveNoWeight()
    {
        var result = RevenueAllocationCalculator.Allocate(
            250m,
            [new RevenueAllocationInput("A", "Module A", 0m)]);

        Assert.Empty(result.Items);
        Assert.Equal(250m, result.UnallocatedAmount);
    }

    [Fact]
    public void CalculatePercentage_ReturnsZeroWhenTotalIsZero()
    {
        Assert.Equal(0m, AnalyticsMetricCalculator.CalculatePercentage(10m, 0m));
        Assert.Equal(33.33m, AnalyticsMetricCalculator.CalculatePercentage(1m, 3m));
    }
}
