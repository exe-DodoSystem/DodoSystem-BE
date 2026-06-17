namespace SMEFLOWSystem.Application.DTOs.HRDtos;

/// <summary>
/// DTO chuyên biệt cho việc cập nhật lương nhân viên.
/// </summary>
public class UpdateSalaryDto
{
    /// <summary>Mức lương cơ bản mới</summary>
    public decimal BaseSalary { get; set; }

    /// <summary>Ngày hiệu lực thay đổi lương (mặc định = hôm nay)</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Lý do thay đổi lương</summary>
    public string? Reason { get; set; }
}
