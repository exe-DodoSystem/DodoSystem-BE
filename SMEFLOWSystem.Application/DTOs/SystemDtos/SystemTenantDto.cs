namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemTenantQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? ModuleId { get; set; }
    public int? ExpiringInDays { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}

public sealed class SystemPeriodQueryDto
{
    public int? Month { get; set; }
    public int? Year { get; set; }
}

public class SystemTenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? SubscriptionEndDate { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SystemTenantModuleDto> Modules { get; set; } = new();
}

public sealed class SystemTenantListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? SubscriptionEndDate { get; set; }
    public int? RemainingDays { get; set; }
    public int ActiveModuleCount { get; set; }
    public int UserCount { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SystemTenantOwnerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
}

public sealed class SystemTenantDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? SubscriptionEndDate { get; set; }
    public int? RemainingDays { get; set; }
    public SystemTenantOwnerDto? Owner { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<SystemTenantModuleDto> Modules { get; set; }
        = Array.Empty<SystemTenantModuleDto>();
}
