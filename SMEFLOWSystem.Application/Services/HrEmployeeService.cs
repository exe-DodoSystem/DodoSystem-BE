using AutoMapper;
using ShareKernel.Common.Enum;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class HrEmployeeService : IHrEmployeeService
{
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IPositionRepository _positionRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrAuthorizationService _hrAuth;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepo;
    private readonly IEmployeeSalaryHistoryRepository _salaryHistoryRepo;
    private readonly ICurrentTenantService _currentTenantService;

    public HrEmployeeService(
        IEmployeeRepository employeeRepo,
        IDepartmentRepository departmentRepo,
        IPositionRepository positionRepo,
        ICurrentUserService currentUser,
        IHrAuthorizationService hrAuth,
        IMapper mapper,
        IUserRepository userRepo,
        IEmployeeSalaryHistoryRepository salaryHistoryRepo,
        ICurrentTenantService currentTenantService)
    {
        _employeeRepo = employeeRepo;
        _departmentRepo = departmentRepo;
        _positionRepo = positionRepo;
        _currentUser = currentUser;
        _hrAuth = hrAuth;
        _mapper = mapper;
        _userRepo = userRepo;
        _salaryHistoryRepo = salaryHistoryRepo;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResultDto<EmployeeDto>> GetPagedAsync(EmployeeQueryDto query)
    {
        _currentUser.EnsureHrAccess();

        // Xác định phạm vi DepartmentId được phép xem
        // - null  = Admin/HRManager → không giới hạn (dùng query.DepartmentId như cũ)
        // - list  = Manager → chỉ trong danh sách phòng ban được giao
        var accessibleIds = await _hrAuth.GetAccessibleDepartmentIdsAsync();

        Guid? departmentId = query.DepartmentId;

        if (accessibleIds != null)
        {
            // Manager scope: nếu request có DepartmentId nhưng không nằm trong danh sách → Forbidden
            if (departmentId.HasValue && !accessibleIds.Contains(departmentId.Value))
                throw new UnauthorizedAccessException("Forbidden");

            // Nếu Manager không filter cụ thể, ta không thể lấy tất cả → cần filter theo danh sách
            // Lưu ý: Repository cần hỗ trợ filter theo nhiều departmentId
            // Nếu chưa có, tạm thời dùng departmentId đầu tiên hoặc loop
            // → Hiện tại nếu Manager có 1 phòng ban, dùng như cũ
            // → Nếu nhiều phòng ban, lấy tất cả và ghép lại
            if (!departmentId.HasValue)
            {
                // Manager không chỉ định → lấy tất cả trong phạm vi cho phép
                if (accessibleIds.Count == 0)
                {
                    return new PagedResultDto<EmployeeDto>
                    {
                        Items = new List<EmployeeDto>(),
                        TotalCount = 0,
                        PageNumber = query.PageNumber,
                        PageSize = query.PageSize
                    };
                }

                if (accessibleIds.Count == 1)
                {
                    departmentId = accessibleIds[0];
                }
                else
                {
                    // Nhiều phòng ban: load từng cái và ghép (đủ dùng cho MVP)
                    var allItems = new List<Employee>();
                    var totalCount = 0;
                    foreach (var deptId in accessibleIds)
                    {
                        var (deptItems, deptTotal) = await _employeeRepo.GetPagedAsync(
                            departmentId: deptId,
                            positionId: query.PositionId,
                            roleId: query.RoleId,
                            status: query.Status,
                            includeResigned: query.IncludeResigned,
                            search: query.Search,
                            pageNumber: 1,
                            pageSize: int.MaxValue,
                            sortBy: query.SortBy,
                            sortDir: query.SortDir);
                        allItems.AddRange(deptItems);
                        totalCount += deptTotal;
                    }
                    // Áp dụng phân trang thủ công
                    var skip = (query.PageNumber - 1) * query.PageSize;
                    var paged = allItems.Skip(skip).Take(query.PageSize).ToList();
                    return new PagedResultDto<EmployeeDto>
                    {
                        Items = _mapper.Map<List<EmployeeDto>>(paged),
                        TotalCount = totalCount,
                        PageNumber = query.PageNumber,
                        PageSize = query.PageSize
                    };
                }
            }
        }

        var (items, total) = await _employeeRepo.GetPagedAsync(
            departmentId: departmentId,
            positionId: query.PositionId,
            roleId: query.RoleId,
            status: query.Status,
            includeResigned: query.IncludeResigned,
            search: query.Search,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            sortBy: query.SortBy,
            sortDir: query.SortDir);

        return new PagedResultDto<EmployeeDto>
        {
            Items = _mapper.Map<List<EmployeeDto>>(items),
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<EmployeeDto> GetByIdAsync(Guid id)
    {
        _currentUser.EnsureHrAccess();
        var emp = await _employeeRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Employee not found");
        await _hrAuth.EnsureEmployeeAccessAsync(emp);
        return _mapper.Map<EmployeeDto>(emp);
    }


    public async Task<EmployeeDto> CreateAsync(EmployeeCreateDto request)
    {
        _currentUser.EnsureHrAccess();

        // Manager chỉ được tạo nhân viên trong phòng ban mình quản lý
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
        {
            if (request.DepartmentId.HasValue)
                await _hrAuth.EnsureDepartmentAccessAsync(request.DepartmentId.Value);
        }

        await ValidateDepartmentPositionAsync(request.DepartmentId, request.PositionId);

        var entity = _mapper.Map<Employee>(request);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.IsDeleted = false;
        entity.Phone = request.Phone ?? string.Empty;
        entity.Email = request.Email ?? string.Empty;

        if (string.Equals(entity.Status, StatusEnum.EmployeeResigned, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Không thể tạo nhân viên với trạng thái Resigned");

        await _employeeRepo.AddAsync(entity);
        var reloaded = await _employeeRepo.GetByIdAsync(entity.Id) ?? entity;
        return _mapper.Map<EmployeeDto>(reloaded);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, EmployeeUpdateDto request)
    {
        _currentUser.EnsureHrAccess();
        var emp = await _employeeRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Employee not found");
        await _hrAuth.EnsureEmployeeAccessAsync(emp);

        // Validate department mới cũng phải trong phạm vi quản lý
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
        {
            if (request.DepartmentId.HasValue)
                await _hrAuth.EnsureDepartmentAccessAsync(request.DepartmentId.Value);
        }

        await ValidateDepartmentPositionAsync(request.DepartmentId, request.PositionId);

        emp.FullName = request.FullName;
        emp.Phone = request.Phone ?? string.Empty;
        emp.Email = request.Email ?? string.Empty;
        emp.DepartmentId = request.DepartmentId;
        emp.PositionId = request.PositionId;
        emp.HireDate = request.HireDate;
        emp.BaseSalary = request.BaseSalary;
        emp.Status = request.Status;
        emp.UserId = request.UserId;

        if (string.Equals(emp.Status, StatusEnum.EmployeeResigned, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.ResignationDate.HasValue)
                throw new ArgumentException("ResignationDate is required when Status=Resigned");
            emp.ResignationDate = request.ResignationDate.Value;

            if (emp.UserId.HasValue)
            {
                var user = await _userRepo.GetUserByIdAsync(emp.UserId.Value);
                if (user != null)
                {
                    user.IsActive = false;
                    await _userRepo.UpdateUserAsync(user);
                }
            }
        }
        else
        {
            emp.ResignationDate = null;

            if (emp.UserId.HasValue)
            {
                var user = await _userRepo.GetUserByIdAsync(emp.UserId.Value);
                if (user != null && !user.IsActive && !user.IsDeleted)
                {
                    user.IsActive = true;
                    await _userRepo.UpdateUserAsync(user);
                }
            }
        }

        emp.UpdatedAt = DateTime.UtcNow;
        await _employeeRepo.UpdateAsync(emp);

        var reloaded = await _employeeRepo.GetByIdAsync(emp.Id) ?? emp;
        return _mapper.Map<EmployeeDto>(reloaded);
    }

    public async Task DeleteAsync(Guid id)
    {
        _currentUser.EnsureHrAccess();
        var emp = await _employeeRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException("Employee not found");
        await _hrAuth.EnsureEmployeeAccessAsync(emp);

        emp.IsDeleted = true;
        emp.UpdatedAt = DateTime.UtcNow;
        await _employeeRepo.UpdateAsync(emp);

        if (emp.UserId.HasValue)
        {
            await _userRepo.SoftDeleteUserAndFreeEmailAsync(emp.UserId.Value);
        }
    }

    public async Task<EmployeeDto> RestoreAsync(Guid id)
    {
        _currentUser.EnsureHrAccess();
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");

        var emp = await _employeeRepo.GetByIdIncludeDeletedAsync(id, tenantId)
            ?? throw new KeyNotFoundException("Employee not found");

        await _hrAuth.EnsureEmployeeAccessAsync(emp);

        emp.IsDeleted = false;
        emp.Status = StatusEnum.EmployeeWorking; // Reset to working status
        emp.ResignationDate = null;
        emp.UpdatedAt = DateTime.UtcNow;
        await _employeeRepo.UpdateAsync(emp);

        if (emp.UserId.HasValue)
        {
            var user = await _userRepo.GetByIdIgnoreTenantAsync(emp.UserId.Value);
            if (user != null && user.TenantId == tenantId)
            {
                user.IsDeleted = false;
                user.IsActive = true;

                if (!string.IsNullOrEmpty(user.Email) && user.Email.Contains(".deleted_"))
                {
                    var index = user.Email.IndexOf(".deleted_");
                    var originalEmail = user.Email.Substring(0, index);
                    if (!await _userRepo.IsEmailExistAsync(originalEmail))
                    {
                        user.Email = originalEmail;
                    }
                }

                await _userRepo.UpdateUserIgnoreTenantAsync(user);
            }
        }

        return _mapper.Map<EmployeeDto>(emp);
    }

    private async Task ValidateDepartmentPositionAsync(Guid? departmentId, Guid? positionId)
    {
        if (!departmentId.HasValue && !positionId.HasValue) return;
        if (!departmentId.HasValue || !positionId.HasValue)
            throw new ArgumentException("DepartmentId và PositionId phải đi cùng nhau");

        var dept = await _departmentRepo.GetByIdAsync(departmentId.Value);
        if (dept == null) throw new ArgumentException("DepartmentId không tồn tại");

        var pos = await _positionRepo.GetByIdAsync(positionId.Value);
        if (pos == null) throw new ArgumentException("PositionId không tồn tại");
        if (pos.DepartmentId != departmentId.Value) throw new ArgumentException("Position không thuộc Department");
    }

    public async Task<List<EmployeeDto>> GetAllByDepartmentId(Guid departmentId)
    {
        _currentUser.EnsureHrAccess();
        await _hrAuth.EnsureDepartmentAccessAsync(departmentId);
        
        var employees = await _employeeRepo.GetByDepartmentIdAsync(departmentId);
        return _mapper.Map<List<EmployeeDto>>(employees);
    }

    public async Task<EmployeeDto> UpdateSalaryAsync(Guid employeeId, UpdateSalaryDto dto)
    {
        // Chỉ Admin hoặc HRManager mới được cập nhật lương
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            throw new UnauthorizedAccessException("Chỉ Admin hoặc HR Manager mới được cập nhật lương.");

        if (dto.BaseSalary < 0)
            throw new ArgumentException("Lương cơ bản không được âm.");

        var emp = await _employeeRepo.GetByIdAsync(employeeId)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");

        await _hrAuth.EnsureEmployeeAccessAsync(emp);

        // Lưu lịch sử thay đổi lương trước khi cập nhật
        var history = new EmployeeSalaryHistory
        {
            Id = Guid.NewGuid(),
            TenantId = emp.TenantId,
            EmployeeId = emp.Id,
            OldSalary = emp.BaseSalary,
            NewSalary = dto.BaseSalary,
            EffectiveDate = dto.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = dto.Reason,
            ChangedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };
        await _salaryHistoryRepo.AddAsync(history);

        emp.BaseSalary = dto.BaseSalary;
        emp.UpdatedAt = DateTime.UtcNow;

        await _employeeRepo.UpdateAsync(emp);

        var reloaded = await _employeeRepo.GetByIdAsync(emp.Id) ?? emp;
        return _mapper.Map<EmployeeDto>(reloaded);
    }

    public async Task<PagedResultDto<EmployeeSalaryHistoryDto>> GetSalaryHistoryPagedAsync(Guid employeeId, int pageNumber, int pageSize)
    {
        // Chỉ Admin hoặc HRManager mới được xem lịch sử lương
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            throw new UnauthorizedAccessException("Chỉ Admin hoặc HR Manager mới được xem lịch sử lương.");

        var emp = await _employeeRepo.GetByIdAsync(employeeId)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");

        await _hrAuth.EnsureEmployeeAccessAsync(emp);

        var (items, total) = await _salaryHistoryRepo.GetPagedByEmployeeIdAsync(employeeId, pageNumber, pageSize);

        return new PagedResultDto<EmployeeSalaryHistoryDto>
        {
            Items = _mapper.Map<List<EmployeeSalaryHistoryDto>>(items),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}

