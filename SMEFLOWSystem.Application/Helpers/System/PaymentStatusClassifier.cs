using System;

namespace SMEFLOWSystem.Application.Helpers.System;

public static class PaymentStatusClassifier
{
    public static bool IsSuccessful(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Settled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFailed(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKnownTerminalStatus(string? status)
    {
        return IsSuccessful(status) || IsFailed(status);
    }
}
