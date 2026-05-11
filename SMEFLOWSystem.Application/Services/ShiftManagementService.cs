using AutoMapper;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class ShiftManagementService : IShiftManagementService
{
    private readonly IShiftRepository _shiftRepo;
    private readonly IShiftPatternRepository _shiftPatternRepo;
    private readonly IShiftAssignmentRepository _shiftAssignmentRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrAuthorizationService _hrAuth;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IMapper _mapper;

    public ShiftManagementService(
        IShiftRepository shiftRepo,
        IShiftPatternRepository shiftPatternRepo,
        IShiftAssignmentRepository shiftAssignmentRepo,
        IEmployeeRepository employeeRepo,
        IHrAuthorizationService hrAuth,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        IMapper mapper)
    {
        _shiftRepo = shiftRepo;
        _shiftPatternRepo = shiftPatternRepo;
        _shiftAssignmentRepo = shiftAssignmentRepo;
        _employeeRepo = employeeRepo;
        _hrAuth = hrAuth;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ShiftDto>> GetPagedAsync(ShiftQueryDto query)
    {
        EnsureHrManagerAccess();

        var (items, total) = await _shiftRepo.GetPagedAsync(
            query.Search,
            query.IncludeDeleted ?? false,
            query.PageNumber,
            query.PageSize);

        return new PagedResultDto<ShiftDto>
        {
            Items = _mapper.Map<List<ShiftDto>>(items),
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<ShiftDto> GetByIdAsync(Guid id)
    {
        EnsureHrManagerAccess();
        var shift = await _shiftRepo.GetByIdWithSegmentsAsync(id)
            ?? throw new KeyNotFoundException("Shift not found");
        return _mapper.Map<ShiftDto>(shift);
    }

    public async Task<ShiftDto> CreateAsync(ShiftCreateDto request)
    {
        EnsureHrManagerAccess();
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        ValidateSegments(request.Segments);

        var shift = _mapper.Map<Shift>(request);
        shift.Id = Guid.NewGuid();
        shift.TenantId = tenantId;
        shift.IsDeleted = false;

        foreach (var segment in shift.Segments)
        {
            segment.Id = Guid.NewGuid();
            segment.ShiftId = shift.Id;
            segment.TenantId = tenantId;
        }

        await _shiftRepo.AddAsync(shift);
        return _mapper.Map<ShiftDto>(shift);
    }

    public async Task<ShiftDto> UpdateAsync(Guid id, ShiftCreateDto request)
    {
        EnsureHrManagerAccess();
        ValidateSegments(request.Segments);

        var shift = await _shiftRepo.GetByIdWithSegmentsAsync(id)
            ?? throw new KeyNotFoundException("Shift not found");

        var hasUsage = await _shiftRepo.HasUsageAsync(id);
        if (hasUsage)
            throw new InvalidOperationException("Ca làm việc đã được sử dụng, không thể chỉnh sửa. Vui lòng tạo ca mới hoặc clone.");

        shift.Code = request.Code;
        shift.Name = request.Name;
        shift.GracePeriodMinutes = request.GracePeriodMinutes;
        shift.IsCrossDay = request.IsCrossDay;

        shift.Segments.Clear();
        foreach (var seg in request.Segments)
        {
            shift.Segments.Add(new ShiftSegment
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                TenantId = shift.TenantId,
                StartTime = seg.StartTime,
                EndTime = seg.EndTime,
                StartDayOffset = seg.StartDayOffset,
                EndDayOffset = seg.EndDayOffset
            });
        }

        await _shiftRepo.UpdateAsync(shift);
        return _mapper.Map<ShiftDto>(shift);
    }

    public async Task DeleteAsync(Guid id)
    {
        EnsureHrManagerAccess();
        var shift = await _shiftRepo.GetByIdWithSegmentsAsync(id)
            ?? throw new KeyNotFoundException("Shift not found");

        var hasUsage = await _shiftRepo.HasUsageAsync(id);
        if (hasUsage)
            throw new InvalidOperationException("Ca làm việc đã được sử dụng, không thể xóa.");

        await _shiftRepo.SoftDeleteAsync(shift);
    }

    public async Task<PagedResultDto<ShiftPatternDto>> GetPatternsPagedAsync(ShiftPatternQueryDto query)
    {
        EnsureHrManagerAccess();

        var (items, total) = await _shiftPatternRepo.GetPagedAsync(
            query.Search,
            query.IncludeDeleted ?? false,
            query.PageNumber,
            query.PageSize);

        return new PagedResultDto<ShiftPatternDto>
        {
            Items = _mapper.Map<List<ShiftPatternDto>>(items),
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<ShiftPatternDto> GetPatternByIdAsync(Guid id)
    {
        EnsureHrManagerAccess();
        var pattern = await _shiftPatternRepo.GetByIdWithDaysAsync(id)
            ?? throw new KeyNotFoundException("Shift pattern not found");
        return _mapper.Map<ShiftPatternDto>(pattern);
    }

    public async Task<ShiftPatternDto> CreatePatternAsync(ShiftPatternCreateDto request)
    {
        EnsureHrManagerAccess();
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        ValidatePattern(request);
        await ValidateShiftIdsAsync(request.Days);

        var pattern = _mapper.Map<ShiftPattern>(request);
        pattern.Id = Guid.NewGuid();
        pattern.TenantId = tenantId;
        pattern.IsDeleted = false;

        foreach (var day in pattern.Days)
        {
            day.Id = Guid.NewGuid();
            day.ShiftPatternId = pattern.Id;
            day.TenantId = tenantId;
        }

        await _shiftPatternRepo.AddAsync(pattern);
        return _mapper.Map<ShiftPatternDto>(pattern);
    }

    public async Task<ShiftPatternDto> UpdatePatternAsync(Guid id, ShiftPatternCreateDto request)
    {
        EnsureHrManagerAccess();

        ValidatePattern(request);
        await ValidateShiftIdsAsync(request.Days);

        var pattern = await _shiftPatternRepo.GetByIdWithDaysAsync(id)
            ?? throw new KeyNotFoundException("Shift pattern not found");

        var hasUsage = await _shiftPatternRepo.HasUsageAsync(id);
        if (hasUsage)
            throw new InvalidOperationException("Lịch ca đã được sử dụng, không thể chỉnh sửa. Vui lòng tạo lịch mới hoặc clone.");

        pattern.Name = request.Name;
        pattern.CycleLengthDays = request.CycleLengthDays;

        pattern.Days.Clear();
        foreach (var day in request.Days)
        {
            pattern.Days.Add(new ShiftPatternDay
            {
                Id = Guid.NewGuid(),
                ShiftPatternId = pattern.Id,
                TenantId = pattern.TenantId,
                DayIndex = day.DayIndex,
                ScheduledShiftId = day.ScheduledShiftId
            });
        }

        await _shiftPatternRepo.UpdateAsync(pattern);
        return _mapper.Map<ShiftPatternDto>(pattern);
    }

    public async Task DeletePatternAsync(Guid id)
    {
        EnsureHrManagerAccess();
        var pattern = await _shiftPatternRepo.GetByIdWithDaysAsync(id)
            ?? throw new KeyNotFoundException("Shift pattern not found");

        var hasUsage = await _shiftPatternRepo.HasUsageAsync(id);
        if (hasUsage)
            throw new InvalidOperationException("Lịch ca đã được sử dụng, không thể xóa.");

        await _shiftPatternRepo.SoftDeleteAsync(pattern);
    }

    public async Task<List<EmployeeShiftPatternDto>> BulkAssignPatternAsync(ShiftAssignmentBulkCreateDto request)
    {
        _currentUser.EnsureHrAccess();

        if (request.EmployeeIds == null || request.EmployeeIds.Count == 0)
            throw new ArgumentException("Must provide at least one employee id");

        var shiftPattern = await _shiftPatternRepo.GetByIdWithDaysAsync(request.ShiftPatternId)
            ?? throw new KeyNotFoundException("Shift pattern not found");

        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        var uniqueEmployeeIds = request.EmployeeIds.Distinct().ToList();
        var employees = await _employeeRepo.GetByIdsAsync(uniqueEmployeeIds);

        if (employees.Count != uniqueEmployeeIds.Count)
            throw new ArgumentException("Danh sách nhân viên không hợp lệ.");

        if (_currentUser.IsManager())
        {
            foreach (var emp in employees)
            {
                await _hrAuth.EnsureEmployeeAccessAsync(emp);
            }
        }

        await _shiftAssignmentRepo.BulkEndPreviousAssignmentsAsync(uniqueEmployeeIds, request.EffectiveStartDate);

        var assignments = employees.Select(emp => new EmployeeShiftPattern
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = emp.Id,
            ShiftPatternId = shiftPattern.Id,
            EffectiveStartDate = request.EffectiveStartDate,
            EffectiveEndDate = null
        }).ToList();

        await _shiftAssignmentRepo.BulkInsertAssignmentsAsync(assignments);
        return _mapper.Map<List<EmployeeShiftPatternDto>>(assignments);
    }

    public async Task<PagedResultDto<EmployeeShiftPatternDto>> GetAssignmentsPagedAsync(ShiftAssignmentQueryDto query)
    {
        _currentUser.EnsureHrAccess();

        var accessibleIds = await _hrAuth.GetAccessibleDepartmentIdsAsync();
        Guid? departmentId = query.DepartmentId;

        if (accessibleIds != null)
        {
            if (departmentId.HasValue && !accessibleIds.Contains(departmentId.Value))
                throw new UnauthorizedAccessException("Forbidden");

            if (query.EmployeeId.HasValue)
            {
                var emp = await _employeeRepo.GetByIdAsync(query.EmployeeId.Value)
                    ?? throw new KeyNotFoundException("Employee not found");
                await _hrAuth.EnsureEmployeeAccessAsync(emp);
            }

            if (!departmentId.HasValue)
            {
                if (accessibleIds.Count == 0)
                {
                    return new PagedResultDto<EmployeeShiftPatternDto>
                    {
                        Items = new List<EmployeeShiftPatternDto>(),
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
                    var allItems = new List<EmployeeShiftPattern>();
                    var totalCount = 0;
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    foreach (var deptId in accessibleIds)
                    {
                        var (deptItems, deptTotal) = await _shiftAssignmentRepo.GetPagedAsync(
                            query.EmployeeId,
                            deptId,
                            query.ShiftPatternId,
                            query.IsActiveOnly,
                            1,
                            int.MaxValue,
                            today);
                        allItems.AddRange(deptItems);
                        totalCount += deptTotal;
                    }

                    var skip = (query.PageNumber - 1) * query.PageSize;
                    var paged = allItems.Skip(skip).Take(query.PageSize).ToList();
                    return new PagedResultDto<EmployeeShiftPatternDto>
                    {
                        Items = _mapper.Map<List<EmployeeShiftPatternDto>>(paged),
                        TotalCount = totalCount,
                        PageNumber = query.PageNumber,
                        PageSize = query.PageSize
                    };
                }
            }
        }

        var (items, total) = await _shiftAssignmentRepo.GetPagedAsync(
            query.EmployeeId,
            departmentId,
            query.ShiftPatternId,
            query.IsActiveOnly,
            query.PageNumber,
            query.PageSize,
            DateOnly.FromDateTime(DateTime.UtcNow));

        return new PagedResultDto<EmployeeShiftPatternDto>
        {
            Items = _mapper.Map<List<EmployeeShiftPatternDto>>(items),
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<EmployeeShiftPatternDto> GetAssignmentByIdAsync(Guid id)
    {
        _currentUser.EnsureHrAccess();
        var assignment = await _shiftAssignmentRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Shift assignment not found");

        if (_currentUser.IsManager())
        {
            if (assignment.Employee == null)
                throw new KeyNotFoundException("Employee not found");
            await _hrAuth.EnsureEmployeeAccessAsync(assignment.Employee);
        }

        return _mapper.Map<EmployeeShiftPatternDto>(assignment);
    }

    private void EnsureHrManagerAccess()
    {
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            throw new UnauthorizedAccessException("Forbidden");
    }

    private static void ValidateSegments(List<ShiftSegmentCreateDto> segments)
    {
        if (segments == null || segments.Count == 0)
            throw new ArgumentException("Segments is required");

        var normalized = segments
            .Select((s, index) => new
            {
                Index = index,
                Start = (s.StartDayOffset * 24 * 60) + s.StartTime.TotalMinutes,
                End = (s.EndDayOffset * 24 * 60) + s.EndTime.TotalMinutes
            })
            .ToList();

        foreach (var seg in normalized)
        {
            if (seg.End <= seg.Start)
                throw new ArgumentException($"Segment[{seg.Index}] không hợp lệ: StartTime phải nhỏ hơn EndTime.");
        }

        var ordered = normalized.OrderBy(x => x.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Start < ordered[i - 1].End)
                throw new ArgumentException("Các segment bị chồng lấn thời gian.");
        }
    }

    private static void ValidatePattern(ShiftPatternCreateDto request)
    {
        if (request.CycleLengthDays <= 0)
            throw new ArgumentException("CycleLengthDays phải lớn hơn 0.");

        if (request.Days == null || request.Days.Count == 0)
            throw new ArgumentException("Days is required");

        var seen = new HashSet<int>();
        foreach (var day in request.Days)
        {
            if (day.DayIndex < 0 || day.DayIndex >= request.CycleLengthDays)
                throw new ArgumentException("DayIndex không hợp lệ.");

            if (!seen.Add(day.DayIndex))
                throw new ArgumentException("DayIndex bị trùng lặp.");
        }
    }

    private async Task ValidateShiftIdsAsync(List<DayCreateDto> days)
    {
        var shiftIds = days
            .Where(d => d.ScheduledShiftId.HasValue)
            .Select(d => d.ScheduledShiftId!.Value)
            .Distinct()
            .ToList();

        foreach (var shiftId in shiftIds)
        {
            var exists = await _shiftPatternRepo.ShiftExistsAsync(shiftId);
            if (!exists)
                throw new ArgumentException($"ScheduledShiftId {shiftId} không tồn tại.");
        }
    }

    
}
