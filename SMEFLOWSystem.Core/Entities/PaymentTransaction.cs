using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Lịch sử giao dịch thanh toán qua cổng thanh toán (Gateway).
    /// </summary>
    public class PaymentTransaction : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        public Guid BillingOrderId { get; set; }

        /// <summary>Cổng thanh toán (VD: VNPay, Momo).</summary>
        public string Gateway { get; set; } = string.Empty;
        /// <summary>Mã giao dịch trên cổng.</summary>
        public string GatewayTransactionId { get; set; } = string.Empty;
        public string? GatewayResponseCode { get; set; }

        /// <summary>Số tiền giao dịch.</summary>
        public decimal Amount { get; set; }
        /// <summary>Trạng thái thanh toán.</summary>
        public string Status { get; set; } = string.Empty;

        public string? RawData { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
