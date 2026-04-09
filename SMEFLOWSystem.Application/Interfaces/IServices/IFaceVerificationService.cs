namespace SMEFLOWSystem.Application.Interfaces.IServices;

/// <summary>
/// Service xác minh khuôn mặt - không phụ thuộc provider cụ thể.
/// </summary>
public interface IFaceVerificationService
{
    /// <summary>
    /// So sánh 2 ảnh khuôn mặt.
    /// </summary>
    /// <param name="selfieUrl">URL ảnh selfie (Cloudinary)</param>
    /// <param name="avatarUrl">URL avatar nhân viên (Cloudinary)</param>
    /// <returns>Kết quả xác minh</returns>
    Task<FaceVerificationResult> VerifyAsync(string selfieUrl, string avatarUrl);
}

/// <summary>
/// Kết quả xác minh khuôn mặt.
/// </summary>
/// <param name="IsMatch">Khuôn mặt khớp hay không (dựa trên threshold)</param>
/// <param name="Confidence">Độ tin cậy (0.0 - 1.0)</param>
/// <param name="ErrorMessage">Thông báo lỗi nếu có (không detect được mặt, v.v.)</param>
public record FaceVerificationResult(bool IsMatch, double Confidence, string? ErrorMessage = null);
