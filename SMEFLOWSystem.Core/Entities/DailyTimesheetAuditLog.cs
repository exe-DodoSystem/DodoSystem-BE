using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public class DailyTimesheetAuditLog
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid DailyTimesheetId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; }    = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Guid ActionByUserId { get; set; }
        public DateTime ActionDate { get; set; }
    }
}
