namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public class SystemTenantModuleDto
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
