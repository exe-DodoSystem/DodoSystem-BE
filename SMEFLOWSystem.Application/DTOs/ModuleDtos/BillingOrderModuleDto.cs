namespace SMEFLOWSystem.Application.DTOs.ModuleDtos;

public class BillingOrderModuleDto
{
    public Guid Id { get; set; }
    public Guid BillingOrderId { get; set; }
    public int ModuleId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? ProrationDays { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}
