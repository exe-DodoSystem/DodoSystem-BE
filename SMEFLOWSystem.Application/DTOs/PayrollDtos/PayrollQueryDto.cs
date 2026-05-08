using System;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class PayrollQueryDto
    {
        public Guid? DepartmentId { get; set; }
        public Guid? EmployeeId { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public string? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public string SortDir { get; set; } = "asc";
    }
}
