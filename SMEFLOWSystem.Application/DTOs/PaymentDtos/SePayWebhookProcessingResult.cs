namespace SMEFLOWSystem.Application.DTOs.PaymentDtos;

public sealed record SePayWebhookProcessingResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? Message = null)
{
    public static SePayWebhookProcessingResult Success() => new(true);

    public static SePayWebhookProcessingResult Failure(string errorCode, string message)
        => new(false, errorCode, message);
}
