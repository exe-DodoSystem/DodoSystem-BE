using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    public partial class OvertimeRequest : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly OvertimeDate { get; set; }
        public decimal RequestedHours { get; set; }
        public string Reason { get; set; }
        public decimal? ApprovedHours { get; set; }
        public string Status { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public decimal? SystemCalculatedMultiplier { get; set; }
    }
}
