using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Lưu vết mọi thay đổi do con người tác động vào bảng chấm công hằng ngày.
    /// </summary>
    public class DailyTimesheetAuditLog
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid DailyTimesheetId { get; set; }
        /// <summary>Trường bị sửa đổi.</summary>
        public string FieldName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; }    = string.Empty;
        /// <summary>Lý do chỉnh sửa.</summary>
        public string Reason { get; set; } = string.Empty;
        public Guid ActionByUserId { get; set; }
        public DateTime ActionDate { get; set; }
    }
}
