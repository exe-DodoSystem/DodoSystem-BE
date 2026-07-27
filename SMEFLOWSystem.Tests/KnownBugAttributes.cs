namespace SMEFLOWSystem.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class KnownBugFactAttribute : FactAttribute
{
    private const string RunKnownBugTestsVariable = "RUN_KNOWN_BUG_TESTS";

    public KnownBugFactAttribute(string gapId)
    {
        if (!IsEnabled())
        {
            Skip =
                $"Known gap {gapId}. Set {RunKnownBugTestsVariable}=1 to run the characterization assertion.";
        }
    }

    private static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(RunKnownBugTestsVariable),
            "1",
            StringComparison.Ordinal);
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable =
        "PHASE6_POSTGRES_CONNECTION_STRING";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip =
                $"Requires a disposable PostgreSQL database. Set {ConnectionStringVariable} to run.";
        }
    }
}
