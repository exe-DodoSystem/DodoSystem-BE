#nullable disable
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

public class TenantAttendanceSetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CheckInRadiusMeters { get; set; } = 100;
    public TimeOnly? WorkStartTime { get; set; }
    public TimeOnly? WorkEndTime { get; set; }
    public TimeSpan DayStartCutOffTime { get; set; } = new TimeSpan(12, 1, 0);
    public int LateThresholdMinutes { get; set; } = 10;
    public int EarlyLeaveThresholdMinutes { get; set; } = 10;
    
    public int MinimumOTMinutes { get; set; } = 30; // Dưới 30p không tính OT
    public int OTBlockMinutes { get; set; } = 30; // Block làm tròn (VD: 30p, 60p)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public virtual Tenant Tenant { get; set; }

    // --- DOMAIN LOGIC: Máy móc không được vòng do ---
    // Hàm này giấu tiệt logic tính toán phức tạp vào trong Entity.
    public decimal CalculateValidOTHours(int actualOTMinutes)
    {
        if (actualOTMinutes < MinimumOTMinutes) 
            return 0m; // Không đủ số phút tối thiểu tối thiểu -> Bỏ.

        if (OTBlockMinutes <= 0) 
            return actualOTMinutes / 60m; // Fallback an toàn nếu chưa cấu hình Block

        // Ví dụ: Làm 80 phút, chia 30p = 2 block (số nguyên). Lấy 2 * 30 = 60 phút hợp lệ.
        int validOTMinutes = (actualOTMinutes / OTBlockMinutes) * OTBlockMinutes;
        
        return Math.Round((decimal)validOTMinutes / 60m, 2); // Trả về số Giờ OT thập phân làm tròn 2 chữ số
    }
}
