namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemPaymentTransactionQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? TenantId { get; set; }
    public Guid? BillingOrderId { get; set; }
    public string? Gateway { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class SystemPaymentTransactionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public Guid BillingOrderId { get; set; }
    public string BillingOrderNumber { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string GatewayTransactionId { get; set; } = string.Empty;
    public string? GatewayResponseCode { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
