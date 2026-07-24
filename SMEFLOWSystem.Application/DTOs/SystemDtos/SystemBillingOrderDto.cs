namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemBillingOrderQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public Guid? TenantId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string SortBy { get; set; } = "billingDate";
    public string SortDirection { get; set; } = "desc";
}

public sealed class SystemBillingOrderListItemDto
{
    public Guid Id { get; set; }
    public string BillingOrderNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public DateTime BillingDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ModuleCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SystemBillingOrderModuleLineDto
{
    public Guid Id { get; set; }
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? ProrationDays { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SystemBillingOrderDetailDto
{
    public Guid Id { get; set; }
    public string BillingOrderNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public DateTime BillingDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<SystemBillingOrderModuleLineDto> Modules { get; set; }
        = Array.Empty<SystemBillingOrderModuleLineDto>();
    public IReadOnlyList<SystemPaymentTransactionDto> PaymentTransactions { get; set; }
        = Array.Empty<SystemPaymentTransactionDto>();
}
