namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public class ModuleCreateDto
{
    public string Code { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
