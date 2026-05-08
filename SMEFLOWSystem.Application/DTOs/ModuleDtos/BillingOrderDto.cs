using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public class BillingOrderDto
{
    public Guid Id { get; set; }
    public string TenantName { get; set; } = string.Empty;

    public string BillingOrderNumber { get; set; } = string.Empty;

    public DateTime BillingDate { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? FinalAmount { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

}
