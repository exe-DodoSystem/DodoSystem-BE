using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

/// <summary>
/// Service phân quyền HR tập trung.
/// Phân quyền theo model chuẩn SaaS:
///   - TenantAdmin  → toàn quyền (null = tất cả phòng ban)
///   - HRManager    → toàn quyền nhân sự (null = tất cả phòng ban)
///   - Manager      → chỉ phòng ban được TenantAdmin giao (qua bảng ManagerDepartment)
///   - Employee/Other → không có quyền HR
/// </summary>
public class HrAuthorizationService : IHrAuthorizationService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IManagerDepartmentRepository _managerDeptRepo;

    public HrAuthorizationService(
        ICurrentUserService currentUser,
        IManagerDepartmentRepository managerDeptRepo)
    {
        _currentUser = currentUser;
        _managerDeptRepo = managerDeptRepo;
    }

    public async Task<List<Guid>?> GetAccessibleDepartmentIdsAsync()
    {
        // TenantAdmin & HRManager: toàn quyền → trả về null (caller hiểu là "lấy tất cả")
        if (_currentUser.IsAdmin() || _currentUser.IsHrManager())
            return null;

        // Manager: chỉ phòng ban được giao
        if (_currentUser.IsManager())
        {
            var userId = _currentUser.RequireUserId();
            var deptIds = await _managerDeptRepo.GetDepartmentIdsByUserIdAsync(userId);
            return deptIds; // Có thể là list rỗng nếu chưa được giao phòng ban nào
        }

        // Không có quyền HR
        throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu HR.");
    }

    public async Task EnsureDepartmentAccessAsync(Guid departmentId)
    {
        // TenantAdmin & HRManager: luôn có quyền
        if (_currentUser.IsAdmin() || _currentUser.IsHrManager())
            return;

        if (_currentUser.IsManager())
        {
            var userId = _currentUser.RequireUserId();
            var hasAccess = await _managerDeptRepo.ExistsAsync(userId, departmentId);
            if (!hasAccess)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập phòng ban này.");
            return;
        }

        throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu HR.");
    }

    public async Task EnsureEmployeeAccessAsync(Employee employee)
    {
        // TenantAdmin & HRManager: luôn có quyền
        if (_currentUser.IsAdmin() || _currentUser.IsHrManager())
            return;

        if (_currentUser.IsManager())
        {
            // Nhân viên chưa được gán phòng ban → Manager không có quyền
            if (!employee.DepartmentId.HasValue)
                throw new UnauthorizedAccessException("Nhân viên chưa được gán phòng ban. Bạn không có quyền truy cập.");

            var userId = _currentUser.RequireUserId();
            var hasAccess = await _managerDeptRepo.ExistsAsync(userId, employee.DepartmentId.Value);
            if (!hasAccess)
                throw new UnauthorizedAccessException("Nhân viên này không thuộc phòng ban bạn quản lý.");
            return;
        }

        throw new UnauthorizedAccessException("Bạn không có quyền truy cập dữ liệu HR.");
    }
}
