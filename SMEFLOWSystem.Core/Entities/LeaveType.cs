using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public class LeaveType : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;           // "ANNUAL", "SICK", "PERSONAL"
        public string Name { get; set; } = string.Empty;           // "Phép năm", "Nghỉ bệnh"
        public int DefaultAnnualDays { get; set; }  // Số ngày mặc định / năm (0 = không giới hạn)
        public bool RequiresApproval { get; set; }  // true = phải duyệt, false = tự duyệt (VD: nghỉ bệnh)
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }
}
