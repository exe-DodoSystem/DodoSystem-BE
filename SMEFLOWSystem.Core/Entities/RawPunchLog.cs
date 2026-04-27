using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Domain Entity: Ghi nhận sự kiện chấm công thô (Raw log) từ Mobile App (GPS/FaceID) hoặc máy chấm công.
/// Nguyên tắc: Bảng này là Append-Only (Chỉ thêm nối tiếp). Tuyệt đối không UPDATE/DELETE.
/// </summary>
public partial class RawPunchLog : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid EmployeeId { get; set; }
    
    // Thời điểm hệ thống ghi nhận người dùng thao tác
    public DateTime Timestamp { get; set; }
    
    // Loại quẹt: "In", "Out", hoặc "Auto" (Để hệ thống tự phân giải là In hay Out dựa vào thuật toán)
    public string PunchType { get; set; } = "Auto";

    // --- DỮ LIỆU ĐỊNH DANH SINH TRẮC / VỊ TRÍ ---
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? SelfieUrl { get; set; }
    
    // Lưu lại dấu vết thiết bị (MAC Address, DeviceName) để chống gian lận dùng 1 điện thoại quẹt cho 10 người
    public string? DeviceId { get; set; } 

    // --- TRẠNG THÁI ENGINE ---
    // Background Job Resolution quét cái này. True = Đã tính toán xong vào DailyTimesheet phân đoạn.
    public bool IsProcessed { get; set; } = false;

    public virtual Employee? Employee { get; set; }
}
