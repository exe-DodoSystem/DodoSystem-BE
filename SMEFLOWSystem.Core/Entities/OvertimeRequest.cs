using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Đơn xin tăng ca (OT).
    /// </summary>
    public partial class OvertimeRequest : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        /// <summary>Ngày xin OT.</summary>
        public DateOnly OvertimeDate { get; set; }
        /// <summary>Số giờ xin OT.</summary>
        public decimal RequestedHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        /// <summary>Số giờ được duyệt.</summary>
        public decimal? ApprovedHours { get; set; }
        public string Status { get; set; } = "Pending";
        public Guid? ApprovedByUserId { get; set; }
        /// <summary>Hệ số nhân.</summary>
        public decimal? SystemCalculatedMultiplier { get; set; }

        public virtual Employee? Employee { get; set; }
        public virtual User? ApprovedByUser { get; set; }
    }
}
