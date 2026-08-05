using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SMEFLOWSystem.WebAPI.Security;

public readonly record struct SePayAuthenticationResult(bool Succeeded, string ErrorCode)
{
    public static SePayAuthenticationResult Success() => new(true, string.Empty);

    public static SePayAuthenticationResult Failure(string errorCode) => new(false, errorCode);
}

public static class SePayWebhookAuthentication
{
    private const int AllowedTimestampSkewSeconds = 300;

    public static SePayAuthenticationResult Authenticate(
        IHeaderDictionary headers,
        ReadOnlySpan<byte> rawBody,
        string? expectedApiKey,
        string? expectedWebhookSecret,
        DateTimeOffset utcNow)
    {
        var signature = headers["X-SePay-Signature"].ToString();
        if (!string.IsNullOrWhiteSpace(signature))
        {
            if (string.IsNullOrWhiteSpace(expectedWebhookSecret))
                return SePayAuthenticationResult.Failure("SEPAY_HMAC_NOT_CONFIGURED");

            return ValidateHmac(
                signature,
                headers["X-SePay-Timestamp"].ToString(),
                rawBody,
                expectedWebhookSecret,
                utcNow);
        }

        var authorization = headers.Authorization.ToString();
        if (authorization.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(expectedApiKey))
                return SePayAuthenticationResult.Failure("SEPAY_API_KEY_NOT_CONFIGURED");

            var providedApiKey = authorization["Apikey ".Length..].Trim();
            return FixedTimeEquals(providedApiKey, expectedApiKey)
                ? SePayAuthenticationResult.Success()
                : SePayAuthenticationResult.Failure("SEPAY_INVALID_API_KEY");
        }

        return SePayAuthenticationResult.Failure("SEPAY_MISSING_AUTHENTICATION");
    }

    private static SePayAuthenticationResult ValidateHmac(
        string providedSignature,
        string timestampHeader,
        ReadOnlySpan<byte> rawBody,
        string secret,
        DateTimeOffset utcNow)
    {
        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
            return SePayAuthenticationResult.Failure("SEPAY_INVALID_TIMESTAMP");

        var currentTimestamp = utcNow.ToUnixTimeSeconds();
        if (timestamp < currentTimestamp - AllowedTimestampSkewSeconds
            || timestamp > currentTimestamp + AllowedTimestampSkewSeconds)
            return SePayAuthenticationResult.Failure("SEPAY_EXPIRED_REQUEST");

        const string signaturePrefix = "sha256=";
        if (!providedSignature.StartsWith(signaturePrefix, StringComparison.OrdinalIgnoreCase))
            return SePayAuthenticationResult.Failure("SEPAY_INVALID_SIGNATURE_FORMAT");

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(providedSignature[signaturePrefix.Length..].Trim());
        }
        catch (FormatException)
        {
            return SePayAuthenticationResult.Failure("SEPAY_INVALID_SIGNATURE_FORMAT");
        }

        var timestampPrefix = Encoding.UTF8.GetBytes(
            timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        var signedPayload = new byte[timestampPrefix.Length + rawBody.Length];
        timestampPrefix.CopyTo(signedPayload, 0);
        rawBody.CopyTo(signedPayload.AsSpan(timestampPrefix.Length));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedHash = hmac.ComputeHash(signedPayload);

        return providedHash.Length == expectedHash.Length
            && CryptographicOperations.FixedTimeEquals(providedHash, expectedHash)
                ? SePayAuthenticationResult.Success()
                : SePayAuthenticationResult.Failure("SEPAY_INVALID_SIGNATURE");
    }

    private static bool FixedTimeEquals(string providedValue, string expectedValue)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedValue);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedValue);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
