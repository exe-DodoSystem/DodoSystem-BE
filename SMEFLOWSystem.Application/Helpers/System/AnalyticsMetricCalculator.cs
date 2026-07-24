using SMEFLOWSystem.Application.Interfaces.IRepositories;

namespace SMEFLOWSystem.Application.Helpers.System;

public static class AnalyticsMetricCalculator
{
    public static decimal CalculateFinalAmount(
        decimal totalAmount,
        decimal? discountAmount,
        decimal? finalAmount)
    {
        return finalAmount ?? (totalAmount - (discountAmount ?? 0m));
    }

    public static decimal SumInvoiced(IEnumerable<InvoicedOrderRow> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        return orders.Sum(order => order.FinalAmount);
    }

    public static decimal SumCollected(IEnumerable<CollectedPaymentRow> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);
        return payments
            .Where(payment =>
                payment.ProcessedAt.HasValue
                && PaymentStatusClassifier.IsSuccessful(payment.Status))
            .Sum(payment => payment.Amount);
    }

    public static decimal CalculateEstimatedMrr(
        IEnumerable<ActiveSubscriptionPriceRow> subscriptions,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        return subscriptions
            .Where(subscription =>
                subscription.StartDate <= asOfUtc
                && subscription.EndDate > asOfUtc
                && string.Equals(
                    subscription.Status,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            .Sum(subscription => subscription.MonthlyPrice);
    }

    public static decimal CalculatePercentage(
        decimal amount,
        decimal total,
        int decimalPlaces = 2)
    {
        if (total == 0m)
        {
            return 0m;
        }
        if (decimalPlaces is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalPlaces),
                "Decimal places must be between zero and six.");
        }

        return Math.Round(
            amount * 100m / total,
            decimalPlaces,
            MidpointRounding.AwayFromZero);
    }

    public static decimal? AverageNonNegative(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var validValues = values.Where(value => value >= 0m).ToList();
        return validValues.Count == 0 ? null : validValues.Average();
    }
}
