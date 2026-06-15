using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public class EmployeeLeaveBalance : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid LeaveTypeId { get; set; }
        public int Year { get; set; }
        public decimal TotalDays { get; set; }    // Tổng ngày được phép trong năm
        public decimal UsedDays { get; set; }     // Đã sử dụng (tính từ Approved leaves)
        public decimal RemainingDays { get; set; }
    }
}
