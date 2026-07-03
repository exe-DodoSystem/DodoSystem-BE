using System;

namespace SMEFLOWSystem.Application.DTOs.PaymentDtos
{
    /// <summary>Response trả về cho Frontend chứa thông tin chuyển khoản SePay</summary>
    public record SePayPaymentInfoDto(
        string TransferContent,      // Nội dung CK: "SUB-xxxxxxx" hoặc "DODO SUB-xxxxxxx"
        string BankAccountNumber,    // Số tài khoản nhận tiền
        string BankAccountName,      // Tên chủ tài khoản
        string BankCode,             // Mã ngân hàng (MB, VCB, TCB, ...)
        decimal Amount,              // Số tiền cần chuyển
        string QrCodeUrl,            // URL ảnh QR từ VietQR
        Guid OrderId                 // ID đơn hàng
    );
}
