using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IManagerDepartmentRepository
{
    /// <summary>Lấy toàn bộ phân công phòng ban của 1 Manager (kèm navigation Department)</summary>
    Task<List<ManagerDepartment>> GetByUserIdAsync(Guid userId);

    /// <summary>Lấy danh sách DepartmentId mà Manager được phép quản lý</summary>
    Task<List<Guid>> GetDepartmentIdsByUserIdAsync(Guid userId);

    /// <summary>Thêm 1 bản ghi phân công mới</summary>
    Task AddAsync(ManagerDepartment entity);

    /// <summary>Xóa 1 phân công cụ thể (UserId + DepartmentId)</summary>
    Task RemoveAsync(Guid userId, Guid departmentId);

    /// <summary>Xóa toàn bộ phân công của 1 Manager (dùng khi Replace)</summary>
    Task RemoveAllByUserIdAsync(Guid userId);

    /// <summary>Kiểm tra đã tồn tại bản ghi phân công chưa</summary>
    Task<bool> ExistsAsync(Guid userId, Guid departmentId);
}
