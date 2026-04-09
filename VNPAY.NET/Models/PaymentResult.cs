namespace VNPAY.NET.Models
{
    /// <summary>
    /// Phản hồi từ VNPAY sau khi thực hiện giao dịch thanh toán.
    /// </summary>
    public class PaymentResult
    {
        public string PaymentId { get; set; }

        public bool IsSuccess { get; set; }

        public string Description { get; set; }

        public DateTime Timestamp { get; set; }
        public long VnpayTransactionId { get; set; }

        public string PaymentMethod { get; set; }
        public PaymentResponse PaymentResponse { get; set; }

        public TransactionStatus TransactionStatus { get; set; }
        public BankingInfor BankingInfor { get; set; }
    }
}