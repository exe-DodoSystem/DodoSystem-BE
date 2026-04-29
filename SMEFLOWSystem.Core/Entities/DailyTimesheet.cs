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
        public string ExpectedShiftSource { get; set; } = string.Empty;

        public decimal StandardWorkingHours { get; set; }
        // Tổng phút làm việc thực tế(trừ giờ nghỉ)
        public int TotalActualWorkedMinutes { get; set; }
        public int TotalLateMinutes { get; set; }
        public int TotalEarlyLeaveMinutes { get; set; }
        public string SystemAnomalyFlag { get; set; } = string.Empty;
        public string ResolutionLogJson { get; set; } = string.Empty;

        public bool IsManuallyAdjusted { get; set; }

        public virtual Employee? Employee { get; set; }
        public virtual Shift? ExpectedShift { get; set; }

        public virtual ICollection<DailyTimesheetSegment> Segments { get; set; } = new List<DailyTimesheetSegment>();
        public virtual ICollection<DailyTimesheetAuditLog> AuditLogs { get; set; } = new List<DailyTimesheetAuditLog>();
    }
}
