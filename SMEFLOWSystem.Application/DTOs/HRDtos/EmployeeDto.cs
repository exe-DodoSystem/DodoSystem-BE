namespace SMEFLOWSystem.Application.DTOs.HRDtos;

public class EmployeeDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }

    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public Guid? PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }
    public DateOnly? ResignationDate { get; set; }
    public decimal BaseSalary { get; set; }
    public string Status { get; set; } = string.Empty;
}
