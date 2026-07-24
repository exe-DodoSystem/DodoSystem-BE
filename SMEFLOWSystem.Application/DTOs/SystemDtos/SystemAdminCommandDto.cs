namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemTenantStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class SystemTenantStatusResultDto
{
    public Guid TenantId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public bool Changed { get; set; }
}

public sealed class SystemSubscriptionExtendRequestDto
{
    public DateTime NewEndDate { get; set; }
    public string? Reason { get; set; }
}

public sealed class SystemSubscriptionReasonRequestDto
{
    public string? Reason { get; set; }
}

public sealed class SystemSubscriptionCommandResultDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int ModuleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool Changed { get; set; }
}
