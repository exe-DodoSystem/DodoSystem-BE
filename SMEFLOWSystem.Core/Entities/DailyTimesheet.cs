using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Bảng công tính tổng theo ngày của 1 nhân sự.
    /// </summary>
    public partial class DailyTimesheet : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly WorkDate { get; set; }

        public Guid? ExpectedShiftId { get; set; }
        public string ExpectedShiftSource { get; set; } = string.Empty;

        /// <summary>Tổng giờ làm chuẩn.</summary>
        public decimal StandardWorkingHours { get; set; }
        // Tổng phút làm việc thực tế(trừ giờ nghỉ)
        public int TotalActualWorkedMinutes { get; set; }
        /// <summary>Giờ thực làm.</summary>
        public decimal ActualWorkHours { get; set; }
        /// <summary>Giờ tăng ca (OT).</summary>
        public decimal OTHours { get; set; }
        /// <summary>Tổng số phút đi trễ.</summary>
        public int TotalLateMinutes { get; set; }
        public int LateMinutes { get; set; }
        /// <summary>Tổng số phút về sớm.</summary>
        public int TotalEarlyLeaveMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        /// <summary>Cờ bất thường của hệ thống (VD: Quên chấm công).</summary>
        public string SystemAnomalyFlag { get; set; } = string.Empty;
        public string ResolutionLogJson { get; set; } = string.Empty;

        public bool IsManuallyAdjusted { get; set; }

        public virtual Employee? Employee { get; set; }
        public virtual Shift? ExpectedShift { get; set; }

        public virtual ICollection<DailyTimesheetSegment> Segments { get; set; } = new List<DailyTimesheetSegment>();
        public virtual ICollection<DailyTimesheetAuditLog> AuditLogs { get; set; } = new List<DailyTimesheetAuditLog>();
    }
}
