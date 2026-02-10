using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities;

public class BillingOrder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string BillingOrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }

    public DateTime BillingDate { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? FinalAmount { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }
}
