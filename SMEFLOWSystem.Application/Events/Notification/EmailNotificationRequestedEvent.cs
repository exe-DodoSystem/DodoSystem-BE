using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Events.Notification
{
    public class EmailNotificationRequestedEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
        public string ToEmail { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;    
        public string Body { get; init; } = string.Empty;
        public string? CorrelationId { get; init; }
        public Guid TenantId { get; init; }
    }
}
