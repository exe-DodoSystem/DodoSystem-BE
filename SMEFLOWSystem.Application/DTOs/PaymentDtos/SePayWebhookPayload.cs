namespace SMEFLOWSystem.Application.DTOs.PaymentDtos
{
    /// <summary>Payload SePay gửi qua webhook khi có giao dịch thành công</summary>
    public record SePayWebhookPayload(
        long Id,                     // Transaction ID trên SePay
        string? Gateway,             // Tên ngân hàng (vd: "MBBank")
        string? TransactionDate,     // Thời gian giao dịch (vd: "2026-07-01 13:00:00")
        string? AccountNumber,       // STK nhận tiền
        string? SubAccount,          // Tài khoản phụ
        decimal TransferAmount,      // Số tiền giao dịch
        decimal Accumulated,         // Số dư tích lũy
        string Code,                 // Mã giao dịch trên SePay
        string Content,              // Nội dung CK
        string? ReferenceCode,       // Mã tham chiếu ngân hàng
        string? Description,         // Mô tả giao dịch
        string TransferType          // "in" = nhận tiền, "out" = chuyển tiền đi
    );
}
