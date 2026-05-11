using AutoMapper;
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
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IMapper _mapper;

    public ShiftManagementService(
        IShiftRepository shiftRepo,
        IShiftPatternRepository shiftPatternRepo,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        IMapper mapper)
    {
        _shiftRepo = shiftRepo;
        _shiftPatternRepo = shiftPatternRepo;
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
