using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Events.Payroll
{
    public class PayrollProcessEvent
    {
        public Guid EventId { get; set; }
        public DateTime OccurredAt { get; set; } 
        public Guid TenantId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string? CorrelationId { get; set; } 

    }
}
