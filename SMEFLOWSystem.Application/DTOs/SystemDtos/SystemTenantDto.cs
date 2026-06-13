namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

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
