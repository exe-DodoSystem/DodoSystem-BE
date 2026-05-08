using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Áp dụng chu kỳ ca cho nhân viên.
    /// </summary>
    public partial class EmployeeShiftPattern : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid ShiftPatternId { get; set; }

        /// <summary>Ngày bắt đầu áp dụng chu kỳ ca.</summary>
        public DateOnly EffectiveStartDate { get; set; }
        /// <summary>Ngày kết thúc áp dụng chu kỳ ca.</summary>
        public DateOnly? EffectiveEndDate { get; set; }

        public virtual Employee? Employee { get; set; }
        public virtual ShiftPattern? ShiftPattern { get; set; }
    }
}
