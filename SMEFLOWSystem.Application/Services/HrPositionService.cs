using AutoMapper;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class HrPositionService : IHrPositionService
{
    private readonly IPositionRepository _positionRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrAuthorizationService _hrAuth;
    private readonly IMapper _mapper;

    public HrPositionService(
        IPositionRepository positionRepo,
        IDepartmentRepository departmentRepo,
        ICurrentUserService currentUser,
        IHrAuthorizationService hrAuth,
        IMapper mapper)
    {
        _positionRepo = positionRepo;
        _departmentRepo = departmentRepo;
        _currentUser = currentUser;
        _hrAuth = hrAuth;
        _mapper = mapper;
    }

    public async Task<List<PositionDto>> GetByDepartmentAsync(Guid departmentId)
    {
        _currentUser.EnsureHrAccess();

        // Validate: Manager chỉ được xem position của phòng ban mình quản lý
        await _hrAuth.EnsureDepartmentAccessAsync(departmentId);

        var items = await _positionRepo.GetByDepartmentIdAsync(departmentId);
        return _mapper.Map<List<PositionDto>>(items);
    }

    public async Task<PositionDto> CreateAsync(PositionCreateDto request)
    {
        _currentUser.EnsureAdmin();
        var dept = await _departmentRepo.GetByIdAsync(request.DepartmentId);
        if (dept == null) throw new KeyNotFoundException("Department not found");

        var entity = _mapper.Map<Position>(request);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.IsDeleted = false;
        await _positionRepo.AddAsync(entity);
        return _mapper.Map<PositionDto>(entity);
    }

    public async Task<PositionDto> UpdateAsync(Guid id, PositionUpdateDto request)
    {
        _currentUser.EnsureAdmin();
        var pos = await _positionRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Position not found");
        var dept = await _departmentRepo.GetByIdAsync(request.DepartmentId);
        if (dept == null) throw new KeyNotFoundException("Department not found");

        pos.Name = request.Name;
        pos.DepartmentId = request.DepartmentId;
        pos.UpdatedAt = DateTime.UtcNow;
        await _positionRepo.UpdateAsync(pos);
        return _mapper.Map<PositionDto>(pos);
    }

    public async Task DeleteAsync(Guid id)
    {
        _currentUser.EnsureAdmin();
        var pos = await _positionRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Position not found");
        var hasEmployees = await _positionRepo.HasEmployeesAsync(id);
        if (hasEmployees)
            throw new ArgumentException("Không thể xóa vì đang có nhân viên sử dụng");
        await _positionRepo.SoftDeleteAsync(pos);
    }
}
