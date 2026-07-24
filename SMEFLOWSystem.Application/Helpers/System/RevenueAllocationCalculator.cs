namespace SMEFLOWSystem.Application.Helpers.System;

public sealed record RevenueAllocationInput(
    string Key,
    string Name,
    decimal Weight);

public sealed record RevenueAllocationItem(
    string Key,
    string Name,
    decimal Amount);

public sealed record RevenueAllocationResult(
    IReadOnlyList<RevenueAllocationItem> Items,
    decimal UnallocatedAmount);

public static class RevenueAllocationCalculator
{
    public static RevenueAllocationResult Allocate(
        decimal amount,
        IEnumerable<RevenueAllocationInput> inputs,
        int decimalPlaces = 2)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }
        if (decimalPlaces is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalPlaces),
                "Decimal places must be between zero and six.");
        }

        var groupedInputs = inputs
            .Select((input, index) => new { Input = input, Index = index })
            .GroupBy(item => item.Input.Key, StringComparer.Ordinal)
            .Select(group => new
            {
                Key = group.Key,
                Name = group.First().Input.Name,
                Weight = group.Sum(item => item.Input.Weight),
                FirstIndex = group.Min(item => item.Index)
            })
            .OrderBy(item => item.FirstIndex)
            .ToList();

        if (groupedInputs.Any(item =>
                string.IsNullOrWhiteSpace(item.Key)
                || item.Weight < 0m))
        {
            throw new ArgumentException(
                "Allocation keys must be non-empty and weights cannot be negative.",
                nameof(inputs));
        }

        var positiveInputs = groupedInputs
            .Where(item => item.Weight > 0m)
            .ToList();
        var totalWeight = positiveInputs.Sum(item => item.Weight);

        if (totalWeight == 0m)
        {
            return new RevenueAllocationResult([], amount);
        }

        var allocations = new List<RevenueAllocationItem>(positiveInputs.Count);
        var allocatedAmount = 0m;
        for (var index = 0; index < positiveInputs.Count; index++)
        {
            var input = positiveInputs[index];
            var isLast = index == positiveInputs.Count - 1;
            var allocated = isLast
                ? amount - allocatedAmount
                : Math.Round(
                    amount * input.Weight / totalWeight,
                    decimalPlaces,
                    MidpointRounding.AwayFromZero);

            allocations.Add(new RevenueAllocationItem(input.Key, input.Name, allocated));
            allocatedAmount += allocated;
        }

        return new RevenueAllocationResult(allocations, 0m);
    }
}
