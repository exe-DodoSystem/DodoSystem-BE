using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class PayrollQueryDto
    {
        //TenantId, DepartmentId, EmployeeId, Month, Year, Status, PageNumber, PageSize, SortBy, SortDir

        public Guid TenantId { get; set; }
        public Guid DepartmentId { get; set; }  
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Status { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = string.Empty;
        public string SortDir { get; set; } = "asc";
    }
}
