namespace SMEFLOWSystem.Application.Helpers.System;

public sealed record RevenueForecastInputPoint(
    DateOnly BucketStart,
    decimal Value);

public sealed record RevenueForecastCalculatedPoint(
    DateOnly BucketStart,
    decimal Value,
    decimal LowerBound,
    decimal UpperBound);

public sealed record RevenueForecastCalculation(
    decimal Slope,
    decimal Intercept,
    decimal ResidualStandardError,
    IReadOnlyList<RevenueForecastCalculatedPoint> Points);

public static class RevenueForecastCalculator
{
    private const decimal ConfidenceMultiplier = 1.96m;

    public static RevenueForecastCalculation Calculate(
        IReadOnlyList<RevenueForecastInputPoint> trainingPoints,
        int forecastPeriods)
    {
        ArgumentNullException.ThrowIfNull(trainingPoints);
        if (trainingPoints.Count < 2)
        {
            throw new ArgumentException(
                "At least two monthly training points are required.",
                nameof(trainingPoints));
        }
        if (forecastPeriods is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(forecastPeriods),
                "Forecast periods must be between one and six.");
        }

        var ordered = trainingPoints
            .OrderBy(point => point.BucketStart)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var point = ordered[index];
            if (point.Value < 0m)
            {
                throw new ArgumentException(
                    "Training values cannot be negative.",
                    nameof(trainingPoints));
            }
            if (point.BucketStart.Day != 1)
            {
                throw new ArgumentException(
                    "Training bucket dates must be the first day of a month.",
                    nameof(trainingPoints));
            }
            if (index > 0
                && point.BucketStart != ordered[index - 1].BucketStart.AddMonths(1))
            {
                throw new ArgumentException(
                    "Training points must contain unique contiguous months.",
                    nameof(trainingPoints));
            }
        }

        var count = ordered.Count;
        var n = (decimal)count;
        var sumX = 0m;
        var sumY = 0m;
        var sumXy = 0m;
        var sumXSquared = 0m;
        for (var index = 0; index < count; index++)
        {
            var x = (decimal)index;
            var y = ordered[index].Value;
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumXSquared += x * x;
        }

        var denominator = n * sumXSquared - sumX * sumX;
        var slope = denominator == 0m
            ? 0m
            : (n * sumXy - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;
        var xMean = sumX / n;
        var centeredXSum = 0m;
        var residualSumSquared = 0m;
        for (var index = 0; index < count; index++)
        {
            var x = (decimal)index;
            var residual = ordered[index].Value - (intercept + slope * x);
            residualSumSquared += residual * residual;
            var centeredX = x - xMean;
            centeredXSum += centeredX * centeredX;
        }

        var residualDegreesOfFreedom = Math.Max(1, count - 2);
        var residualStandardError = DecimalSquareRoot(
            residualSumSquared / residualDegreesOfFreedom);
        var points = new List<RevenueForecastCalculatedPoint>(forecastPeriods);
        var lastMonth = ordered[^1].BucketStart;
        for (var offset = 1; offset <= forecastPeriods; offset++)
        {
            var x = (decimal)(count - 1 + offset);
            var predicted = intercept + slope * x;
            var leverage = 1m + 1m / n;
            if (centeredXSum > 0m)
            {
                var centeredForecastX = x - xMean;
                leverage += centeredForecastX
                    * centeredForecastX
                    / centeredXSum;
            }

            var margin = ConfidenceMultiplier
                * residualStandardError
                * DecimalSquareRoot(leverage);
            var value = RoundCurrency(Math.Max(0m, predicted));
            var lowerBound = RoundCurrency(Math.Max(0m, predicted - margin));
            var upperBound = RoundCurrency(Math.Max(value, predicted + margin));
            points.Add(new RevenueForecastCalculatedPoint(
                lastMonth.AddMonths(offset),
                value,
                lowerBound,
                upperBound));
        }

        return new RevenueForecastCalculation(
            slope,
            intercept,
            residualStandardError,
            points);
    }

    private static decimal DecimalSquareRoot(decimal value)
    {
        return value <= 0m ? 0m : (decimal)Math.Sqrt((double)value);
    }

    private static decimal RoundCurrency(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
