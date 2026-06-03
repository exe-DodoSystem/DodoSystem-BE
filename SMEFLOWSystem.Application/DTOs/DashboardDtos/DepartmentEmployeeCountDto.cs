using System;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class DepartmentEmployeeCountDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int Count { get; set; }
}
