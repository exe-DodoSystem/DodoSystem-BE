namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public sealed class ModuleUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
}
