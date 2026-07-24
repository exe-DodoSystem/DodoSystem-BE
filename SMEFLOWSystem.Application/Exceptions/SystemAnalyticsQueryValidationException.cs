namespace SMEFLOWSystem.Application.Exceptions;

public sealed class SystemAnalyticsQueryValidationException : Exception
{
    public SystemAnalyticsQueryValidationException(string propertyName, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = [message]
        };
    }

    public IDictionary<string, string[]> Errors { get; }
}
