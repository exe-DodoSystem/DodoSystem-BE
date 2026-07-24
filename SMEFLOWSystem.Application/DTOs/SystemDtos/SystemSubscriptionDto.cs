namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemSubscriptionQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTenant { get; set; }
    public Guid? TenantId { get; set; }
    public int? ModuleId { get; set; }
    public string? Status { get; set; }
    public bool IncludeCancelled { get; set; }
    public DateTime? ExpiringFrom { get; set; }
    public DateTime? ExpiringTo { get; set; }
}

public sealed class SystemSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int RemainingDays { get; set; }
}
