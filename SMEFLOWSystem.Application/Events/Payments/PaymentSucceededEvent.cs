namespace SMEFLOWSystem.Application.Events.Payments;

public sealed class PaymentSucceededEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public Guid BillingOrderId { get; init; }
    public Guid TenantId { get; init; }
    public string Gateway { get; init; } = "VNPay";
    public string GatewayTransactionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string? CorrelationId { get; init; }
}
