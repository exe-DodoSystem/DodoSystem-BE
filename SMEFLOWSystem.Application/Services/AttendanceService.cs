using AutoMapper;
using ShareKernel.Common.Enum;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using Microsoft.Extensions.Configuration;

namespace SMEFLOWSystem.Application.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IAttendanceSettingRepository _settingRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ICloudinaryService _cloudinary;
    private readonly IFaceVerificationService _faceService;
    private readonly IMapper _mapper;

    public AttendanceService(
        IAttendanceRepository attendanceRepo,
        IAttendanceSettingRepository settingRepo,
        IEmployeeRepository employeeRepo,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        ICloudinaryService cloudinary,
        IFaceVerificationService faceService,
        IMapper mapper)
    {
        _attendanceRepo = attendanceRepo;
        _settingRepo = settingRepo;
        _employeeRepo = employeeRepo;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _cloudinary = cloudinary;
        _faceService = faceService;
        _mapper = mapper;
        
    }

    public async Task<CheckInResponseDto> CheckInAsync(CheckInRequestDto request)
    {
        var userId = _currentUser.RequireUserId();
        var employee = await RequireEmployeeAsync(userId);
        var setting = await RequireSettingAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Validate: chưa check-in hôm nay
        var existing = await _attendanceRepo.GetTodayByEmployeeIdAsync(employee.Id, today);
        if (existing != null)
            throw new InvalidOperationException("Bạn đã check-in hôm nay rồi.");

        // Validate GPS
        ValidateGps(request.Latitude, request.Longitude, setting);

        // Tính Late
        var checkInTime = DateTime.UtcNow;
        var status = StatusEnum.AttendancePresent;
        int? lateMinutes = null;

        if (setting.WorkStartTime.HasValue)
        {
            var deadline = setting.WorkStartTime.Value.AddMinutes(setting.LateThresholdMinutes);
            var currentTime = TimeOnly.FromDateTime(checkInTime);
            if (currentTime > deadline)
            {
                status = StatusEnum.AttendanceLate;
                // Dùng TimeSpan thay vì TimeOnly - TimeOnly để tránh wrap-around 24h
                var diff = currentTime.ToTimeSpan() - setting.WorkStartTime.Value.ToTimeSpan();
                lateMinutes = (int)Math.Max(0, diff.TotalMinutes);
            }
        }

        string? selfieUrl = !string.IsNullOrEmpty(request.SelfieBase64)
            ? await _cloudinary.UploadBase64Async(request.SelfieBase64, "attendance/checkin")
            : null;

        // Face verification
        await VerifyFaceAsync(selfieUrl, employee);

        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId!.Value,
            EmployeeId = employee.Id,
            WorkDate = today,
            CheckInTime = checkInTime,
            CheckInLatitude = request.Latitude,
            CheckInLongitude = request.Longitude,
            CheckInSelfieUrl = selfieUrl,
            Status = status,
            LateMinutes = lateMinutes,
            CreatedAt = DateTime.UtcNow
        };

        await _attendanceRepo.AddAsync(attendance);
        return _mapper.Map<CheckInResponseDto>(attendance);
    }

    public async Task<CheckOutResponseDto> CheckOutAsync(CheckOutRequestDto request)
    {
        var userId = _currentUser.RequireUserId();
        var employee = await RequireEmployeeAsync(userId);
        var setting = await RequireSettingAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attendance = await _attendanceRepo.GetTodayByEmployeeIdAsync(employee.Id, today)
            ?? throw new KeyNotFoundException("Bạn chưa check-in hôm nay.");

        if (attendance.CheckOutTime.HasValue)
            throw new InvalidOperationException("Bạn đã check-out hôm nay rồi.");

        // Validate GPS
        ValidateGps(request.Latitude, request.Longitude, setting);

        var checkOutTime = DateTime.UtcNow;

        // Upload selfie
        string? selfieUrl = !string.IsNullOrEmpty(request.SelfieBase64)
            ? await _cloudinary.UploadBase64Async(request.SelfieBase64, "attendance/checkout")
            : null;

        // Face verification
        await VerifyFaceAsync(selfieUrl, employee);

        attendance.CheckOutTime = checkOutTime;
        attendance.CheckOutLatitude = request.Latitude;
        attendance.CheckOutLongitude = request.Longitude;
        attendance.CheckOutSelfieUrl = selfieUrl;
        attendance.UpdatedAt = DateTime.UtcNow;

        // Tính EarlyLeave
        if (setting.WorkEndTime.HasValue)
        {
            var currentTime = TimeOnly.FromDateTime(checkOutTime);
            var earlyDeadline = setting.WorkEndTime.Value.AddMinutes(-setting.EarlyLeaveThresholdMinutes);
            if (currentTime < earlyDeadline)
            {
                // Dùng TimeSpan thay vì TimeOnly - TimeOnly để tránh wrap-around 24h
                var diff = setting.WorkEndTime.Value.ToTimeSpan() - currentTime.ToTimeSpan();
                attendance.EarlyLeaveMinutes = (int)Math.Max(0, diff.TotalMinutes);
                attendance.ApprovalStatus = StatusEnum.ApprovalPending;
            }
        }

        await _attendanceRepo.UpdateAsync(attendance);
        return _mapper.Map<CheckOutResponseDto>(attendance);
    }

    public async Task<AttendanceStatusDto> GetMyStatusAsync(DateOnly date)
    {
        var userId = _currentUser.RequireUserId();
        var employee = await RequireEmployeeAsync(userId);
        var setting = await _settingRepo.GetByTenantIdAsync(_currentTenant.TenantId!.Value);

        var todayRecord = await _attendanceRepo.GetTodayByEmployeeIdAsync(employee.Id, date);

        return new AttendanceStatusDto
        {
            TodayAttendance = todayRecord != null ? _mapper.Map<AttendanceDto>(todayRecord) : null,
            OfficeLatitude = setting?.Latitude,
            OfficeLongitude = setting?.Longitude,
            CheckInRadiusMeters = setting?.CheckInRadiusMeters ?? 100,
            WorkStartTime = setting?.WorkStartTime,
            WorkEndTime = setting?.WorkEndTime
        };
    }

    public async Task<TodayAttendanceDto?> GetTodayStatusAsync()
    {
        var userId = _currentUser.RequireUserId();
        var employee = await RequireEmployeeAsync(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var record = await _attendanceRepo.GetTodayByEmployeeIdAsync(employee.Id, today);
        if (record == null)
            return null;

        return _mapper.Map<TodayAttendanceDto>(record);
    }

    public async Task<List<AttendanceDto>> GetMyHistoryAsync(DateOnly from, DateOnly to)
    {
        var userId = _currentUser.RequireUserId();
        var employee = await RequireEmployeeAsync(userId);
        var records = await _attendanceRepo.GetByEmployeeIdAsync(employee.Id, from, to);
        return _mapper.Map<List<AttendanceDto>>(records);
    }

    public async Task<AttendanceDto> GetByIdAsync(Guid id)
    {
        _currentUser.RequireUserId();
        var record = await _attendanceRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy bản ghi chấm công.");
        return _mapper.Map<AttendanceDto>(record);
    }

    public async Task<PagedResultDto<AttendanceDto>> GetPagedAsync(AttendanceQueryDto query)
    {
        var userId = _currentUser.RequireUserId();
        Guid? deptId = query.DepartmentId;

        // Manager chỉ xem được dept của mình
        if (_currentUser.IsManager() && !_currentUser.IsAdmin())
        {
            var emp = await _employeeRepo.GetByUserIdAsync(userId);
            if (emp == null || !emp.DepartmentId.HasValue)
                throw new UnauthorizedAccessException("Bạn chưa được gán phòng ban.");
            deptId = emp.DepartmentId.Value;
        }
        else if (!_currentUser.IsAdmin())
        {
            throw new UnauthorizedAccessException("Forbidden");
        }

        var (items, total) = await _attendanceRepo.GetPagedAsync(
            deptId,
            query.EmployeeId,
            query.FromDate,
            query.ToDate,
            query.Status,
            query.ApprovalStatus,
            query.Search,
            query.PageNumber,
            query.PageSize,
            query.SortBy,
            query.SortDir
        );

        return new PagedResultDto<AttendanceDto>
        {
            Items = _mapper.Map<List<AttendanceDto>>(items),
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<AttendanceDto> ApproveAsync(Guid attendanceId, AttendanceApproveDto dto)
    {
        var userId = _currentUser.RequireUserId();

        if (!_currentUser.IsAdmin() && !_currentUser.IsManager())
            throw new UnauthorizedAccessException("Forbidden");

        var record = await _attendanceRepo.GetByIdAsync(attendanceId)
            ?? throw new KeyNotFoundException("Không tìm thấy bản ghi chấm công.");

        // Self-approve prevention
        var myEmployee = await _employeeRepo.GetByUserIdAsync(userId);
        if (myEmployee != null && myEmployee.Id == record.EmployeeId)
            throw new UnauthorizedAccessException("Không được tự approve bản thân.");

        // Manager chỉ approve được dept của mình
        if (_currentUser.IsManager() && !_currentUser.IsAdmin())
        {
            var myEmp = myEmployee ?? throw new UnauthorizedAccessException("Bạn chưa liên kết Employee.");
            if (!myEmp.DepartmentId.HasValue)
                throw new UnauthorizedAccessException("Bạn chưa được gán phòng ban.");

            var targetEmp = await _employeeRepo.GetByIdAsync(record.EmployeeId);
            if (targetEmp?.DepartmentId != myEmp.DepartmentId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể approve nhân viên trong phòng ban của mình.");
        }

        // Validate ApprovalStatus
        if (dto.ApprovalStatus != StatusEnum.ApprovalApproved && dto.ApprovalStatus != StatusEnum.ApprovalRejected)
            throw new ArgumentException("ApprovalStatus phải là 'Approved' hoặc 'Rejected'.");

        if (dto.ApprovalStatus == StatusEnum.ApprovalRejected && string.IsNullOrWhiteSpace(dto.ApprovalNotes))
            throw new ArgumentException("Cần nhập lý do khi từ chối.");

        record.ApprovalStatus = dto.ApprovalStatus;
        record.ApprovalNotes = dto.ApprovalNotes;
        record.ApprovedByUserId = userId;
        record.ApprovedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;

        // Admin correction: override time nếu có
        if (dto.CheckInTime.HasValue) record.CheckInTime = dto.CheckInTime;
        if (dto.CheckOutTime.HasValue) record.CheckOutTime = dto.CheckOutTime;

        await _attendanceRepo.UpdateAsync(record);
        return _mapper.Map<AttendanceDto>(record);
    }

    public async Task<AttendanceConfigResponseDto> GetConfigAsync()
    {
        var tenantId = _currentTenant.TenantId ?? throw new UnauthorizedAccessException("Unauthenticated");
        var setting = await _settingRepo.GetByTenantIdAsync(tenantId);

        if (setting == null)
            return new AttendanceConfigResponseDto { CheckInRadiusMeters = 100, LateThresholdMinutes = 10, EarlyLeaveThresholdMinutes = 10 };

        return _mapper.Map<AttendanceConfigResponseDto>(setting);
    }

    public async Task<AttendanceConfigResponseDto> UpsertConfigAsync(AttendanceConfigDto dto)
    {
        _currentUser.EnsureAdmin();

        var tenantId = _currentTenant.TenantId ?? throw new UnauthorizedAccessException("Unauthenticated");
        var existing = await _settingRepo.GetByTenantIdAsync(tenantId);

        if (existing == null)
        {
            existing = new TenantAttendanceSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };
        }

        _mapper.Map(dto, existing);
        existing.UpdatedAt = DateTime.UtcNow;

        await _settingRepo.UpsertAsync(existing);
        return _mapper.Map<AttendanceConfigResponseDto>(existing);
    }

    private async Task<Employee> RequireEmployeeAsync(Guid userId)
    {
        return await _employeeRepo.GetByUserIdAsync(userId)
            ?? throw new UnauthorizedAccessException("Tài khoản chưa liên kết Employee.");
    }

    private async Task<TenantAttendanceSetting> RequireSettingAsync()
    {
        var tenantId = _currentTenant.TenantId ?? throw new UnauthorizedAccessException("Unauthenticated");
        var setting = await _settingRepo.GetByTenantIdAsync(tenantId)
            ?? throw new InvalidOperationException("Công ty chưa cấu hình vị trí chấm công.");
        if (!setting.Latitude.HasValue || !setting.Longitude.HasValue)
            throw new InvalidOperationException("Công ty chưa cấu hình tọa độ vị trí chấm công.");
        return setting;
    }

    private static void ValidateGps(double userLat, double userLng, TenantAttendanceSetting setting)
    {
        var distance = GeoHelper.DistanceInMeters(
            userLat, userLng,
            setting.Latitude!.Value, setting.Longitude!.Value);

        if (distance > setting.CheckInRadiusMeters)
            throw new InvalidOperationException(
                $"Vị trí không hợp lệ. Bạn đang cách văn phòng {distance:F0}m (tối đa {setting.CheckInRadiusMeters}m).");
    }

    private async Task VerifyFaceAsync(string? selfieUrl, Employee employee)
    {
        if (string.IsNullOrEmpty(selfieUrl)) return;
        var avatarUrl = employee.User?.AvatarUrl;
        if (string.IsNullOrEmpty(avatarUrl)) return;

        var result = await _faceService.VerifyAsync(selfieUrl, avatarUrl);
        if (!result.IsMatch)
        {
            try { await _cloudinary.DeleteAsync(selfieUrl); } catch { }

            var message = !string.IsNullOrEmpty(result.ErrorMessage)
                ? result.ErrorMessage
                : $"Xác minh khuôn mặt thất bại (confidence: {result.Confidence:P0}).";
            throw new InvalidOperationException(message);
        }
    }
}
