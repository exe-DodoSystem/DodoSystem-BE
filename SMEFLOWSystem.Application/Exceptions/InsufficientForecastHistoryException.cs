namespace SMEFLOWSystem.Application.Exceptions;

public sealed class InsufficientForecastHistoryException : Exception
{
    public InsufficientForecastHistoryException(
        int requiredMonths,
        int availableMonths)
        : base(
            $"Revenue forecast requires {requiredMonths} complete contiguous months, "
            + $"but only {availableMonths} were available.")
    {
        RequiredMonths = requiredMonths;
        AvailableMonths = availableMonths;
    }

    public int RequiredMonths { get; }
    public int AvailableMonths { get; }
}
