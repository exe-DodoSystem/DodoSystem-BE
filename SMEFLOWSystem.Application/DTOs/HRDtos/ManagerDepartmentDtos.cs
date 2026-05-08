namespace SMEFLOWSystem.Application.DTOs.HRDtos;

/// <summary>Thông tin 1 phòng ban được gán cho Manager (dùng cho response)</summary>
public class ManagerDepartmentDto
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
}

/// <summary>Request gán/thay thế danh sách phòng ban cho Manager</summary>
public class AssignManagerDepartmentDto
{
    /// <summary>Danh sách DepartmentId muốn gán (dùng cho Assign và Replace)</summary>
    public List<Guid> DepartmentIds { get; set; } = new();
}
