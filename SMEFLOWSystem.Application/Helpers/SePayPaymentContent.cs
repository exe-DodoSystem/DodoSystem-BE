using System.Text.RegularExpressions;

namespace SMEFLOWSystem.Application.Helpers;

public static class SePayPaymentContent
{
    private static readonly Regex BillingOrderNumberRegex = new(
        @"(SUB|BO)[\s\-_.]*(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex NonAlphaNumericRegex = new(
        @"[^A-Z0-9]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string BuildTransferContent(string? prefix, string billingOrderNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(billingOrderNumber);

        var bankSafeOrderNumber = NonAlphaNumericRegex.Replace(billingOrderNumber, string.Empty)
            .ToUpperInvariant();
        var bankSafePrefix = string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : NonAlphaNumericRegex.Replace(prefix, string.Empty).ToUpperInvariant();

        return string.IsNullOrEmpty(bankSafePrefix)
            ? bankSafeOrderNumber
            : $"{bankSafePrefix} {bankSafeOrderNumber}";
    }

    public static bool TryExtractBillingOrderNumber(string? transferContent, out string billingOrderNumber)
    {
        billingOrderNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(transferContent))
            return false;

        var match = BillingOrderNumberRegex.Match(transferContent);
        if (!match.Success)
            return false;

        billingOrderNumber = $"{match.Groups[1].Value.ToUpperInvariant()}-{match.Groups[2].Value}";
        return true;
    }
}
