using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities
{
    public class PaymentTransaction : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        public Guid BillingOrderId { get; set; }

        public string Gateway { get; set; } = string.Empty;
        public string GatewayTransactionId { get; set; } = string.Empty;
        public string? GatewayResponseCode { get; set; }

        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;

        public string? RawData { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
