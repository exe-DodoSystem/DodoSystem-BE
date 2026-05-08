using SMEFLOWSystem.Application.DTOs.HRDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IManagerDepartmentService
{
    /// <summary>Lấy danh sách phòng ban mà Manager đang được giao quản lý</summary>
    Task<List<ManagerDepartmentDto>> GetByManagerAsync(Guid userId);

    /// <summary>Gán Manager vào một hoặc nhiều phòng ban (bỏ qua nếu đã tồn tại)</summary>
    Task AssignAsync(Guid userId, AssignManagerDepartmentDto request);

    /// <summary>Gỡ quyền Manager khỏi 1 phòng ban cụ thể</summary>
    Task UnassignAsync(Guid userId, Guid departmentId);

    /// <summary>Thay thế toàn bộ danh sách phòng ban của Manager</summary>
    Task ReplaceAsync(Guid userId, AssignManagerDepartmentDto request);
}
