using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public partial class ShiftPatternDay : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ShiftPatternId { get; set; }
        public int DayIndex { get; set; }
        public Guid? ScheduledShiftId { get; set; }

        public virtual ShiftPattern? ShiftPattern { get; set; }
        public virtual Shift? ScheduledShift { get; set; }
    }
}
