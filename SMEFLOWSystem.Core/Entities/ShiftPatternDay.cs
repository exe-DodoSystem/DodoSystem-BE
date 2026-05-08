using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Từng ngày trong chu kỳ sẽ làm ca nào.
    /// </summary>
    public partial class ShiftPatternDay : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ShiftPatternId { get; set; }
        /// <summary>Ngày thứ mấy trong chu kỳ.</summary>
        public int DayIndex { get; set; }
        /// <summary>Ca làm việc được phân công cho ngày này.</summary>
        public Guid? ScheduledShiftId { get; set; }

        public virtual ShiftPattern? ShiftPattern { get; set; }
        public virtual Shift? ScheduledShift { get; set; }
    }
}
