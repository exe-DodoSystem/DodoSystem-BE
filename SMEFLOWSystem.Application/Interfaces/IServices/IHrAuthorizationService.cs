using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

/// <summary>
/// Service phân quyền HR tập trung — thay thế cho logic GetManagerDepartmentIdOrThrowAsync()
/// bị copy-paste ở nhiều nơi trong codebase cũ.
/// </summary>
public interface IHrAuthorizationService
{
    /// <summary>
    /// Trả về danh sách DepartmentId mà user hiện tại được phép truy cập.
    /// - TenantAdmin hoặc HRManager: trả về null (tức là "tất cả phòng ban")
    /// - Manager: trả về danh sách DepartmentId được TenantAdmin giao từ bảng ManagerDepartment
    /// - Employee hoặc không có quyền: throw UnauthorizedAccessException
    /// </summary>
    Task<List<Guid>?> GetAccessibleDepartmentIdsAsync();

    /// <summary>
    /// Kiểm tra user hiện tại có quyền truy cập vào departmentId cụ thể không.
    /// Throw UnauthorizedAccessException nếu không có quyền.
    /// </summary>
    Task EnsureDepartmentAccessAsync(Guid departmentId);

    /// <summary>
    /// Kiểm tra user hiện tại có quyền truy cập vào employee cụ thể không
    /// (dựa trên DepartmentId của employee).
    /// Throw UnauthorizedAccessException nếu không có quyền.
    /// </summary>
    Task EnsureEmployeeAccessAsync(Employee employee);
}
