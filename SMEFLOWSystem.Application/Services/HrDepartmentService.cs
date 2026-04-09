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
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public HrDepartmentService(
        IDepartmentRepository departmentRepo,
        IEmployeeRepository employeeRepo,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _departmentRepo = departmentRepo;
        _employeeRepo = employeeRepo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<List<DepartmentDto>> GetAccessibleAsync()
    {
        _currentUser.EnsureHrAccess();

        if (_currentUser.IsAdmin())
        {
            var all = await _departmentRepo.GetAllAsync();
            return _mapper.Map<List<DepartmentDto>>(all);
        }

        var myDeptId = await GetManagerDepartmentIdOrThrowAsync();
        var dept = await _departmentRepo.GetByIdAsync(myDeptId) ?? throw new KeyNotFoundException("Department not found");
        return new List<DepartmentDto> { _mapper.Map<DepartmentDto>(dept) };
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

    private async Task<Guid> GetManagerDepartmentIdOrThrowAsync()
    {
        var userId = _currentUser.RequireUserId();
        var emp = await _employeeRepo.GetByUserIdAsync(userId);
        if (emp == null || !emp.DepartmentId.HasValue || !emp.PositionId.HasValue)
            throw new UnauthorizedAccessException("Bạn chưa được gán phòng ban/chức vụ");
        return emp.DepartmentId.Value;
    }
}
