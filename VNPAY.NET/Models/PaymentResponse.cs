using VNPAY.NET.Enums;

namespace VNPAY.NET.Models
{
    
    // Thông tin phản hồi thanh toán
    public class PaymentResponse
    {

        // Mã phản hồi từ hệ thống do VNPAY định nghĩa. 
        public ResponseCode Code { get; set; }

        // Mô tả chi tiết về mã phản hồi, cung cấp thông tin bổ sung về trạng thái giao dịch.
        public string Description { get; set; } = string.Empty;
    }
}
