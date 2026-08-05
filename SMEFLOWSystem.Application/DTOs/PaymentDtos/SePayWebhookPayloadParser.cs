using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace SMEFLOWSystem.Application.DTOs.PaymentDtos;

public readonly record struct SePayWebhookParseResult(
    SePayWebhookPayload? Payload,
    string DetectedFields)
{
    public bool Succeeded => Payload != null;
}

public static class SePayWebhookPayloadParser
{
    public static SePayWebhookParseResult Parse(string rawBody)
    {
        var root = JObject.Parse(rawBody);
        var payloadObject = GetObject(root, "data") ?? root;
        var detectedFields = string.Join(
            ",",
            payloadObject.Properties().Select(property => property.Name).OrderBy(name => name));

        var transactionId = GetString(payloadObject, "id", "transaction_id", "transactionId");
        if (string.IsNullOrWhiteSpace(transactionId))
            return new SePayWebhookParseResult(null, detectedFields);

        var transferType = GetString(payloadObject, "transferType", "transfer_type") ?? string.Empty;
        transferType = transferType.ToLowerInvariant() switch
        {
            "credit" => "in",
            "debit" => "out",
            _ => transferType
        };

        var payload = new SePayWebhookPayload(
            Id: transactionId,
            Gateway: GetString(payloadObject, "gateway", "bank_brand_name", "bankBrandName"),
            TransactionDate: GetString(payloadObject, "transactionDate", "transaction_date"),
            AccountNumber: GetString(payloadObject, "accountNumber", "account_number"),
            SubAccount: GetString(payloadObject, "subAccount", "sub_account", "va"),
            TransferAmount: GetDecimal(
                payloadObject,
                "transferAmount",
                "transfer_amount",
                "amount",
                "amount_in"),
            Accumulated: GetDecimal(payloadObject, "accumulated"),
            Code: GetString(payloadObject, "code", "payment_code", "paymentCode"),
            Content: GetString(payloadObject, "content", "transaction_content", "transactionContent") ?? string.Empty,
            ReferenceCode: GetString(
                payloadObject,
                "referenceCode",
                "reference_code",
                "reference_number",
                "referenceNumber"),
            Description: GetString(payloadObject, "description"),
            TransferType: transferType);

        return new SePayWebhookParseResult(payload, detectedFields);
    }

    private static JObject? GetObject(JObject source, string propertyName)
        => source.GetValue(propertyName, StringComparison.OrdinalIgnoreCase) as JObject;

    private static string? GetString(JObject source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var token = source.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
                continue;

            var value = token.Type == JTokenType.String
                ? token.Value<string>()
                : token.ToString(Formatting.None);

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static decimal GetDecimal(JObject source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var token = source.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
                continue;

            if (token.Type is JTokenType.Integer or JTokenType.Float)
                return token.Value<decimal>();

            if (decimal.TryParse(
                    token.Value<string>(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }
        }

        return 0m;
    }
}
