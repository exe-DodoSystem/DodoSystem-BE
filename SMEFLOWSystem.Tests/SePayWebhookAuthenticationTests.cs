using Microsoft.AspNetCore.Http;
using SMEFLOWSystem.Application.DTOs.PaymentDtos;
using SMEFLOWSystem.WebAPI.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SMEFLOWSystem.Tests;

public sealed class SePayWebhookAuthenticationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_786_000_000);

    [Fact]
    public void OfficialWebhookJson_DeserializesTransactionId()
    {
        const string body = """
            {
              "id": 92704,
              "gateway": "Vietcombank",
              "transactionDate": "2024-07-02 11:08:33",
              "accountNumber": "1017588888",
              "subAccount": "",
              "code": null,
              "content": "DODO SUB-12345678",
              "transferType": "in",
              "description": "NGUYEN VAN A chuyen tien",
              "transferAmount": 5000000,
              "accumulated": 105000000,
              "referenceCode": "FT24012345678"
            }
            """;

        var result = SePayWebhookPayloadParser.Parse(body);

        Assert.True(result.Succeeded);
        var payload = Assert.IsType<SePayWebhookPayload>(result.Payload);
        Assert.Equal("92704", payload.Id);
        Assert.Equal("DODO SUB-12345678", payload.Content);
        Assert.Equal(5_000_000m, payload.TransferAmount);
    }

    [Fact]
    public void BankHubWebhookJson_NormalizesSnakeCasePayload()
    {
        const string body = """
            {
              "gateway": "MBBank",
              "transaction_date": "2026-08-05 10:00:00",
              "account_number": "123456789",
              "va": null,
              "payment_code": null,
              "content": "DODO SUB-12345678",
              "transfer_type": "credit",
              "amount": 250000,
              "reference_code": "FT260805123",
              "accumulated": 1000000,
              "transaction_id": "a47b6ab2-8f8b-11f0-b21a-a6006ab65aca"
            }
            """;

        var result = SePayWebhookPayloadParser.Parse(body);

        Assert.True(result.Succeeded);
        var payload = Assert.IsType<SePayWebhookPayload>(result.Payload);
        Assert.Equal("a47b6ab2-8f8b-11f0-b21a-a6006ab65aca", payload.Id);
        Assert.Equal("in", payload.TransferType);
        Assert.Equal(250_000m, payload.TransferAmount);
        Assert.Equal("DODO SUB-12345678", payload.Content);
    }

    [Fact]
    public void WrappedWebhookJson_ReadsPayloadFromDataObject()
    {
        const string body = """
            {
              "status": "success",
              "data": {
                "id": "transaction-123",
                "gateway": "MBBank",
                "transaction_content": "DODO SUB-12345678",
                "transfer_type": "in",
                "amount_in": 300000
              }
            }
            """;

        var result = SePayWebhookPayloadParser.Parse(body);

        Assert.True(result.Succeeded);
        var payload = Assert.IsType<SePayWebhookPayload>(result.Payload);
        Assert.Equal("transaction-123", payload.Id);
        Assert.Equal("DODO SUB-12345678", payload.Content);
        Assert.Equal(300_000m, payload.TransferAmount);
    }

    [Fact]
    public void Authenticate_AcceptsStandardApiKeyHeader()
    {
        var headers = new HeaderDictionary
        {
            ["Authorization"] = "Apikey test-api-key"
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes("{}"),
            expectedApiKey: "test-api-key",
            expectedWebhookSecret: "also-configured-but-not-required",
            utcNow: Now);

        Assert.True(result.Succeeded);
        Assert.Empty(result.ErrorCode);
    }

    [Fact]
    public void Authenticate_RejectsNonStandardApiKeyScheme()
    {
        var headers = new HeaderDictionary
        {
            ["Authorization"] = "Sepay test-api-key"
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes("{}"),
            expectedApiKey: "test-api-key",
            expectedWebhookSecret: null,
            utcNow: Now);

        Assert.False(result.Succeeded);
        Assert.Equal("SEPAY_MISSING_AUTHENTICATION", result.ErrorCode);
    }

    [Fact]
    public void Authenticate_AcceptsSePayHmacSignature()
    {
        const string body = "{\"id\":92704,\"content\":\"DODO SUB-12345678\"}";
        const string secret = "test-webhook-secret";
        var timestamp = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = ComputeSignature(timestamp, body, secret);
        var headers = new HeaderDictionary
        {
            ["X-SePay-Timestamp"] = timestamp,
            ["X-SePay-Signature"] = signature
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes(body),
            expectedApiKey: "also-configured-but-not-required",
            expectedWebhookSecret: secret,
            utcNow: Now);

        Assert.True(result.Succeeded);
        Assert.Empty(result.ErrorCode);
    }

    [Fact]
    public void Authenticate_RejectsBodyOnlyLegacyHmacSignature()
    {
        const string body = "{\"id\":92704}";
        const string secret = "test-webhook-secret";
        var timestamp = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var legacySignature = "sha256=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var headers = new HeaderDictionary
        {
            ["X-SePay-Timestamp"] = timestamp,
            ["X-SePay-Signature"] = legacySignature
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes(body),
            expectedApiKey: null,
            expectedWebhookSecret: secret,
            utcNow: Now);

        Assert.False(result.Succeeded);
        Assert.Equal("SEPAY_INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public void Authenticate_RejectsExpiredHmacRequest()
    {
        const string body = "{}";
        const string secret = "test-webhook-secret";
        var oldTimestamp = Now.AddMinutes(-6).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var headers = new HeaderDictionary
        {
            ["X-SePay-Timestamp"] = oldTimestamp,
            ["X-SePay-Signature"] = ComputeSignature(oldTimestamp, body, secret)
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes(body),
            expectedApiKey: null,
            expectedWebhookSecret: secret,
            utcNow: Now);

        Assert.False(result.Succeeded);
        Assert.Equal("SEPAY_EXPIRED_REQUEST", result.ErrorCode);
    }

    [Fact]
    public void Authenticate_RejectsExtremeTimestampWithoutThrowing()
    {
        var headers = new HeaderDictionary
        {
            ["X-SePay-Timestamp"] = long.MinValue.ToString(CultureInfo.InvariantCulture),
            ["X-SePay-Signature"] = "sha256=" + new string('0', 64)
        };

        var result = SePayWebhookAuthentication.Authenticate(
            headers,
            Encoding.UTF8.GetBytes("{}"),
            expectedApiKey: null,
            expectedWebhookSecret: "test-webhook-secret",
            utcNow: Now);

        Assert.False(result.Succeeded);
        Assert.Equal("SEPAY_INVALID_TIMESTAMP", result.ErrorCode);
    }

    private static string ComputeSignature(string timestamp, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
