using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Events.Payroll
{
    public class PayrollCalculatedEvent
    {
        public Guid EventId { get; set; }
        public Guid TenantId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalEmloyeesProcessed { get; set; }
        public string TenantAdminEmail { get; set; } = string.Empty;

    }
}
