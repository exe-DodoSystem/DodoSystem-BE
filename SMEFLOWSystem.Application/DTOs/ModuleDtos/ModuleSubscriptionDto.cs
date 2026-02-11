namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public class ModuleSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int ModuleId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
