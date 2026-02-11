namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public class ModuleDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
