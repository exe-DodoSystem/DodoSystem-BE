using AutoMapper;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
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
    private readonly IPublicHolidayRepository _holidayRepo;
    private readonly IHrAuthorizationService _hrAuth;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IMapper _mapper;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<ShiftManagementService> _logger;

    public ShiftManagementService(
        IShiftRepository shiftRepo,
        IShiftPatternRepository shiftPatternRepo,
        IShiftAssignmentRepository shiftAssignmentRepo,
        IEmployeeRepository employeeRepo,
        IPublicHolidayRepository holidayRepo,
        IHrAuthorizationService hrAuth,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        IMapper mapper,
        IRealtimeNotificationService realtime,
        ILogger<ShiftManagementService> logger)
    {
        _shiftRepo = shiftRepo;
        _shiftPatternRepo = shiftPatternRepo;
        _shiftAssignmentRepo = shiftAssignmentRepo;
        _employeeRepo = employeeRepo;
        _holidayRepo = holidayRepo;
        _hrAuth = hrAuth;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _mapper = mapper;
        _realtime = realtime;
        _logger = logger;
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
        var isExistingCodeOrName = await _shiftRepo.IsCodeOrNameExists(request.Code, request.Name);
        if(isExistingCodeOrName)
            throw new ArgumentException("Code hoặc Name đã tồn tại");

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

        await _shiftRepo.DeleteAsync(shift);
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
        if (request.CycleLengthDays > 7)
            throw new ArgumentException("Độ dài của chu kỳ chỉ được bằng 7 hoặc dưới 7");

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

        var createdPattern = await _shiftPatternRepo.GetByIdWithDaysAsync(pattern.Id);
        return _mapper.Map<ShiftPatternDto>(createdPattern ?? pattern);
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

        await _shiftPatternRepo.DeletePatternDaysAsync(pattern.Id);
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

        await _shiftPatternRepo.DeleteAsync(pattern);
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

        // Emit realtime notifications
        var hrName = "HR Manager";
        if (_currentUser.UserId.HasValue)
        {
            var hrEmp = await _employeeRepo.GetByUserIdAsync(_currentUser.UserId.Value);
            if (hrEmp != null)
            {
                hrName = hrEmp.FullName;
            }
        }

        foreach (var emp in employees)
        {
            if (emp.UserId.HasValue)
            {
                var shiftAssignedDto = new
                {
                    shiftPatternId = shiftPattern.Id,
                    shiftPatternName = shiftPattern.Name,
                    effectiveStartDate = request.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    assignedBy = hrName
                };

                _ = _realtime.NotifyShiftAssignedAsync(emp.UserId.Value, shiftAssignedDto)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogWarning(t.Exception, "Notify shift.assigned failed for employee {UserId}", emp.UserId.Value);
                    });
            }
        }

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

    public async Task<MyCurrentShiftAssignmentDto?> GetMyCurrentAssignmentAsync(Guid userId)
    {
        var employee = await _employeeRepo.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ nhân sự cho tài khoản này.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (items, _) = await _shiftAssignmentRepo.GetPagedAsync(
            employeeId: employee.Id,
            departmentId: null,
            shiftPatternId: null,
            isActiveOnly: true,
            pageNumber: 1,
            pageSize: 1,
            today: today);

        var activeAssignment = items.FirstOrDefault();
        if (activeAssignment == null) return null;

        var baseDto = _mapper.Map<EmployeeShiftPatternDto>(activeAssignment);
        var dto = new MyCurrentShiftAssignmentDto
        {
            Id = baseDto.Id,
            EmployeeId = baseDto.EmployeeId,
            EmployeeName = baseDto.EmployeeName,
            EmployeeDepartment = baseDto.EmployeeDepartment,
            ShiftPatternId = baseDto.ShiftPatternId,
            ShiftPatternName = baseDto.ShiftPatternName,
            EffectiveStartDate = baseDto.EffectiveStartDate,
            EffectiveEndDate = baseDto.EffectiveEndDate
        };

        // Nạp riêng thông tin chi tiết đầy đủ của Lịch ca (gồm Days -> Shifts -> Segments)
        var patternDetails = await _shiftPatternRepo.GetByIdWithDaysAsync(activeAssignment.ShiftPatternId);
        if (patternDetails != null)
        {
            dto.ShiftPattern = _mapper.Map<ShiftPatternDto>(patternDetails);
        }

        return dto;
    }

    public async Task<MyScheduleDto?> GetMyScheduleAsync(Guid userId, DateOnly? fromDate, DateOnly? toDate, bool includeOffDays)
    {
        var employee = await _employeeRepo.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ nhân sự cho tài khoản này.");

        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("TenantId không xác định.");

        var start = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = toDate ?? start.AddDays(30);

        if (start > end)
        {
            throw new ArgumentException("Từ ngày (fromDate) không được lớn hơn Đến ngày (toDate).");
        }

        if (end.DayNumber - start.DayNumber > 90)
        {
            throw new ArgumentException("Khoảng thời gian truy vấn không được vượt quá 90 ngày.");
        }

        // 1. Lấy tất cả active patterns trong range
        var activePatterns = await _shiftPatternRepo.GetActivePatternsForEmployeesAsync(
            new List<Guid> { employee.Id },
            start,
            end);

        // Lấy assignment mới nhất trùng khớp với range
        var assignment = activePatterns
            .OrderByDescending(x => x.EffectiveStartDate)
            .FirstOrDefault();

        if (assignment == null)
        {
            return null;
        }

        // 2. Load Pattern chi tiết
        var pattern = await _shiftPatternRepo.GetByIdWithDaysAsync(assignment.ShiftPatternId);
        if (pattern == null)
        {
            return null;
        }

        // 3. Load Holidays
        var holidays = await _holidayRepo.GetAllAsync(tenantId);

        // 4. Lặp qua từng ngày trong range để expand ca làm
        var daysList = new List<WorkDayDto>();
        
        // Chỉ duyệt các ngày trong khoảng giao giữa range yêu cầu và range active của assignment
        var effectiveFrom = start > assignment.EffectiveStartDate ? start : assignment.EffectiveStartDate;
        var effectiveTo = (assignment.EffectiveEndDate.HasValue && assignment.EffectiveEndDate.Value < end)
            ? assignment.EffectiveEndDate.Value
            : end;

        for (var date = effectiveFrom; date <= effectiveTo; date = date.AddDays(1))
        {
            // Tính vị trí trong chu kỳ (tính từ ngày bắt đầu gán lịch)
            var rawPos = date.DayNumber - assignment.EffectiveStartDate.DayNumber;
            var cycleLength = pattern.CycleLengthDays;
            
            // Tránh chia lấy dư ra số âm trong C#
            var cyclePos = ((rawPos % cycleLength) + cycleLength) % cycleLength;

            var patternDay = pattern.Days.FirstOrDefault(d => d.DayIndex == cyclePos);
            var isWorkDay = patternDay?.ScheduledShiftId != null;

            // Tìm ngày lễ
            var holiday = holidays.FirstOrDefault(h =>
                h.IsRecurringYearly
                    ? (h.Date.Month == date.Month && h.Date.Day == date.Day)
                    : h.Date == date
            );
            var isHoliday = holiday != null;

            // Chỉ thêm ngày nghỉ nếu includeOffDays = true, hoặc nếu là ngày làm việc
            if (includeOffDays || isWorkDay)
            {
                var workDay = new WorkDayDto
                {
                    Date = date,
                    DayOfWeekVi = ToDayOfWeekVi(date.DayOfWeek),
                    IsWorkDay = isWorkDay,
                    IsHoliday = isHoliday,
                    HolidayName = holiday?.Name,
                    ShiftName = patternDay?.ScheduledShift?.Name,
                    ShiftCode = patternDay?.ScheduledShift?.Code,
                    Segments = patternDay?.ScheduledShift != null 
                        ? _mapper.Map<List<ShiftSegmentDto>>(patternDay.ScheduledShift.Segments)
                        : new List<ShiftSegmentDto>()
                };
                daysList.Add(workDay);
            }
        }

        return new MyScheduleDto
        {
            AssignmentId = assignment.Id,
            ShiftPatternName = pattern.Name,
            EffectiveStartDate = assignment.EffectiveStartDate,
            EffectiveEndDate = assignment.EffectiveEndDate,
            FromDate = start,
            ToDate = end,
            TotalWorkDays = daysList.Count(d => d.IsWorkDay),
            Days = daysList
        };
    }

    private static string ToDayOfWeekVi(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => string.Empty
        };
    }
}
