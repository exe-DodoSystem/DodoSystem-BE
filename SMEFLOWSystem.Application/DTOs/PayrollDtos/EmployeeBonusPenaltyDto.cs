namespace SMEFLOWSystem.Application.DTOs.PayrollDtos;

/// <summary>
/// DTO gán thưởng/phạt cho một nhân viên theo tháng/năm.
/// Không cần biết payrollId — hệ thống sẽ tự tìm hoặc tạo Payroll Draft.
/// </summary>
public class EmployeeBonusPenaltyDto
{
    /// <summary>ID nhân viên</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Tháng tính lương</summary>
    public int Month { get; set; }

    /// <summary>Năm tính lương</summary>
    public int Year { get; set; }

    /// <summary>Tiền thưởng (null = không thay đổi)</summary>
    public decimal? CustomBonus { get; set; }

    /// <summary>Tiền phạt/khấu trừ (null = không thay đổi)</summary>
    public decimal? CustomDeduction { get; set; }

    /// <summary>Lý do thưởng/phạt</summary>
    public string? Reason { get; set; }
}
