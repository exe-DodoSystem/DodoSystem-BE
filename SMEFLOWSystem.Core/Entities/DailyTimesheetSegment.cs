using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Bảng chi tiết từng khung giờ quẹt In/Out ứng với ShiftSegment.
    /// </summary>
    public partial class DailyTimesheetSegment : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid DailyTimesheetId { get; set; }

        public Guid? TargetShiftSegmentId { get; set; }
        public DateTime? ActualCheckIn { get; set; }
        public DateTime? ActualCheckOut { get; set; }

        public double? CheckInLatitude { get; set; }
        public double? CheckInLongitude { get; set; }
        public string CheckInSelfieUrl { get; set; } = string.Empty;

        public double? CheckOutLatitude { get; set; }
        public double? CheckOutLongitude { get; set; }
        public string CheckOutSelfieUrl { get; set; } = string.Empty;

        /// <summary>Số phút đi trễ cho khung giờ này.</summary>
        public int LateMinutes { get; set; }
        /// <summary>Số phút về sớm cho khung giờ này.</summary>
        public int EarlyLeaveMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
