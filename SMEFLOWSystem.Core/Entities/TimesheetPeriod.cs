using SMEFLOWSystem.SharedKernel.Interfaces;
using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Khái niệm cốt lõi: KỲ CÔNG CHẤM CÔNG (Timesheet Period).
/// Sinh ra để giải quyết bài toán: Chốt lương xong cấm ông nào vào sửa số liệu mập mờ.
/// </summary>
public partial class TimesheetPeriod : ITenantEntity
{
    // Phải để public set cho một số property do ràng buộc của Interface
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    // Các property Domain thì đóng gói lại
    public int Month { get; private set; }
    public int Year { get; private set; }
    
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    
    public bool IsLocked { get; private set; }
    public DateTime? LockedAt { get; private set; }
    public Guid? LockedByUserId { get; private set; }

    /// <summary>
    /// Bắt buộc phải có constructor rỗng vì EF Core cần nó
    /// </summary>
    protected TimesheetPeriod() { }

    // Constructor chuẩn DDD: Chỉ sinh ra kỳ công thông qua nghiệp vụ rõ ràng
    public TimesheetPeriod(Guid tenantId, int month, int year, DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
            throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        Month = month;
        Year = year;
        StartDate = startDate;
        EndDate = endDate;
        IsLocked = false;
    }

    /// <summary>
    /// Nghiệp vụ: Chốt sổ kỳ công
    /// </summary>
    public void LockPeriod(Guid lockedByUserId)
    {
        if (IsLocked)
            throw new InvalidOperationException("Kỳ công này đã bị khóa từ trước, định chốt thêm lần nữa à?");
            
        IsLocked = true;
        LockedAt = DateTime.UtcNow;
        LockedByUserId = lockedByUserId;
    }

    /// <summary>
    /// Phương thức kiểm tra: Có quyền sửa timesheet không?
    /// Nếu bị Locked rồi mà gọi hàm này thì nó văng Exception ngay lập tức.
    /// </summary>
    public void EnsureNotLocked()
    {
        if (IsLocked)
            throw new InvalidOperationException($"Toàn bô dữ liệu công từ {StartDate:dd/MM} đến {EndDate:dd/MM} đã khóa sổ. Cấm chỉnh sửa.");
    }
}
