using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

/// <summary>
/// Service quản lý việc TenantAdmin giao phòng ban cho Manager.
/// Chỉ TenantAdmin mới được gọi các thao tác ghi (Assign, Unassign, Replace).
/// </summary>
public class ManagerDepartmentService : IManagerDepartmentService
{
    private readonly IManagerDepartmentRepository _managerDeptRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;

    public ManagerDepartmentService(
        IManagerDepartmentRepository managerDeptRepo,
        IDepartmentRepository departmentRepo,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant)
    {
        _managerDeptRepo = managerDeptRepo;
        _departmentRepo = departmentRepo;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    /// <inheritdoc />
    public async Task<List<ManagerDepartmentDto>> GetByManagerAsync(Guid userId)
    {
        // TenantAdmin hoặc HRManager được xem, hoặc Manager xem chính mình
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
        {
            var callerId = _currentUser.RequireUserId();
            if (callerId != userId)
                throw new UnauthorizedAccessException("Forbidden");
        }

        var assignments = await _managerDeptRepo.GetByUserIdAsync(userId);
        return assignments.Select(md => new ManagerDepartmentDto
        {
            UserId = md.UserId,
            DepartmentId = md.DepartmentId,
            DepartmentName = md.Department?.Name ?? string.Empty,
            AssignedAt = md.AssignedAt,
            AssignedByUserId = md.AssignedByUserId
        }).ToList();
    }

    /// <inheritdoc />
    public async Task AssignAsync(Guid userId, AssignManagerDepartmentDto request)
    {
        _currentUser.EnsureAdmin();

        await ValidateTargetIsManagerAsync(userId);

        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        var assignedBy = _currentUser.RequireUserId();
        var now = DateTime.UtcNow;

        foreach (var deptId in request.DepartmentIds.Distinct())
        {
            // Validate department tồn tại trong tenant
            var dept = await _departmentRepo.GetByIdAsync(deptId)
                ?? throw new ArgumentException($"Department {deptId} không tồn tại.");

            // Bỏ qua nếu đã được gán rồi
            var exists = await _managerDeptRepo.ExistsAsync(userId, deptId);
            if (exists) continue;

            await _managerDeptRepo.AddAsync(new ManagerDepartment
            {
                UserId = userId,
                DepartmentId = deptId,
                TenantId = tenantId,
                AssignedAt = now,
                AssignedByUserId = assignedBy
            });
        }
    }

    /// <inheritdoc />
    public async Task UnassignAsync(Guid userId, Guid departmentId)
    {
        _currentUser.EnsureAdmin();
        await _managerDeptRepo.RemoveAsync(userId, departmentId);
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(Guid userId, AssignManagerDepartmentDto request)
    {
        _currentUser.EnsureAdmin();

        await ValidateTargetIsManagerAsync(userId);

        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        var assignedBy = _currentUser.RequireUserId();
        var now = DateTime.UtcNow;

        // Validate tất cả department trước khi xóa
        var distinctIds = request.DepartmentIds.Distinct().ToList();
        foreach (var deptId in distinctIds)
        {
            _ = await _departmentRepo.GetByIdAsync(deptId)
                ?? throw new ArgumentException($"Department {deptId} không tồn tại.");
        }

        // Xóa toàn bộ phân công cũ
        await _managerDeptRepo.RemoveAllByUserIdAsync(userId);

        // Thêm danh sách mới
        foreach (var deptId in distinctIds)
        {
            await _managerDeptRepo.AddAsync(new ManagerDepartment
            {
                UserId = userId,
                DepartmentId = deptId,
                TenantId = tenantId,
                AssignedAt = now,
                AssignedByUserId = assignedBy
            });
        }
    }

    /// <summary>Validate user target phải tồn tại và có role Manager</summary>
    private async Task ValidateTargetIsManagerAsync(Guid userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} không tồn tại.");

        var isManager = user.UserRoles.Any(ur =>
            string.Equals(ur.Role?.Name, RoleConstants.Manager, StringComparison.OrdinalIgnoreCase));

        if (!isManager)
            throw new ArgumentException("User này không có role Manager. Chỉ Manager mới có thể được giao phòng ban.");
    }
}
