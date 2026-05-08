using AutoMapper;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class HrDepartmentService : IHrDepartmentService
{
    private readonly IDepartmentRepository _departmentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrAuthorizationService _hrAuth;
    private readonly IMapper _mapper;

    public HrDepartmentService(
        IDepartmentRepository departmentRepo,
        ICurrentUserService currentUser,
        IHrAuthorizationService hrAuth,
        IMapper mapper)
    {
        _departmentRepo = departmentRepo;
        _currentUser = currentUser;
        _hrAuth = hrAuth;
        _mapper = mapper;
    }

    public async Task<List<DepartmentDto>> GetAccessibleAsync()
    {
        _currentUser.EnsureHrAccess();

        // GetAccessibleDepartmentIdsAsync() trả về:
        //   null   → TenantAdmin / HRManager → lấy tất cả
        //   list   → Manager → chỉ các phòng ban được giao
        var accessibleIds = await _hrAuth.GetAccessibleDepartmentIdsAsync();

        if (accessibleIds == null)
        {
            var all = await _departmentRepo.GetAllAsync();
            return _mapper.Map<List<DepartmentDto>>(all);
        }

        if (accessibleIds.Count == 0)
            return new List<DepartmentDto>(); // Manager chưa được giao phòng ban nào

        var departments = new List<DepartmentDto>();
        foreach (var deptId in accessibleIds)
        {
            var dept = await _departmentRepo.GetByIdAsync(deptId);
            if (dept != null)
                departments.Add(_mapper.Map<DepartmentDto>(dept));
        }
        return departments;
    }

    public async Task<DepartmentDto> CreateAsync(DepartmentCreateDto request)
    {
        _currentUser.EnsureAdmin();
        var entity = _mapper.Map<Department>(request);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.IsDeleted = false;
        await _departmentRepo.AddAsync(entity);
        return _mapper.Map<DepartmentDto>(entity);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, DepartmentUpdateDto request)
    {
        _currentUser.EnsureAdmin();
        var dept = await _departmentRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Department not found");
        dept.Name = request.Name;
        dept.UpdatedAt = DateTime.UtcNow;
        await _departmentRepo.UpdateAsync(dept);
        return _mapper.Map<DepartmentDto>(dept);
    }

    public async Task DeleteAsync(Guid id)
    {
        _currentUser.EnsureAdmin();
        var dept = await _departmentRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Department not found");
        var hasEmployees = await _departmentRepo.HasEmployeesAsync(id);
        if (hasEmployees)
            throw new ArgumentException("Không thể xóa vì đang có nhân viên sử dụng");

        await _departmentRepo.SoftDeleteAsync(dept);
    }
}
