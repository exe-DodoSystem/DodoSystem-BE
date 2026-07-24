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
