using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public partial class DailyTimesheet : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly WorkDate { get; set; }

        public Guid? ExpectedShiftId { get; set; }
        public string ExpectedShiftSource { get; set; }

        public decimal StandardWorkingHours { get; set; }
        public int TotalLateMinutes { get; set; }
        public int TotalEarlyLeaveMinutes { get; set; }
        public string SystemAnomalyFlag { get; set; }
        public string ResolutionLogJson { get; set; }

        public bool IsManuallyAdjusted { get; set; }

        public virtual ICollection<DailyTimesheetSegment> Segments { get; set; } = new List<DailyTimesheetSegment>();
        public virtual ICollection<DailyTimesheetAuditLog> AuditLogs { get; set; } = new List<DailyTimesheetAuditLog>();
    }
}
