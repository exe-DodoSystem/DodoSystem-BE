namespace SMEFLOWSystem.Application.DTOs.PayrollDtos;

/// <summary>
/// DTO gán thưởng/phạt hàng loạt cho nhiều nhân viên cùng lúc.
/// </summary>
public class BulkBonusPenaltyDto
{
    /// <summary>Danh sách thưởng/phạt cho từng nhân viên</summary>
    public List<EmployeeBonusPenaltyDto> Items { get; set; } = new();
}
