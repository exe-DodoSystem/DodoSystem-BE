using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.Leave;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly ILeaveBalanceRepository _leaveBalanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ITransaction _transaction;
    private readonly IRawPunchLogRepository _rawPunchLogRepository;
    private readonly IAttendanceSettingRepository _attendanceSettingRepository;

    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    public LeaveRequestService(
        ILeaveRequestRepository leaveRequestRepository,
        ILeaveTypeRepository leaveTypeRepository,
        ILeaveBalanceRepository leaveBalanceRepository,
        IEmployeeRepository employeeRepository,
        ICurrentTenantService currentTenantService,
        ITransaction transaction,
        IRawPunchLogRepository rawPunchLogRepository,
        IAttendanceSettingRepository attendanceSettingRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveBalanceRepository = leaveBalanceRepository;
        _employeeRepository = employeeRepository;
        _currentTenantService = currentTenantService;
        _transaction = transaction;
        _rawPunchLogRepository = rawPunchLogRepository;
        _attendanceSettingRepository = attendanceSettingRepository;
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
    }

    public async Task<LeaveRequestDto> SubmitLeaveRequestAsync(Guid userId, SubmitLeaveRequestDto dto)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");
        var employee = await _employeeRepository.GetByUserIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy thông tin nhân viên cho tài khoản này.");

        var leaveType = await _leaveTypeRepository.GetByIdAsync(dto.LeaveTypeId) ?? throw new InvalidOperationException("Loại nghỉ phép không tồn tại hoặc đã bị xóa.");
        if (!leaveType.IsActive)
        {
            throw new InvalidOperationException("Loại nghỉ phép hiện không hoạt động.");
        }

        if (dto.Days == null || dto.Days.Count == 0)
        {
            throw new InvalidOperationException("Đơn xin nghỉ phép phải có ít nhất một ngày nghỉ.");
        }

        // Validate dates
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        foreach (var day in dto.Days)
        {
            if (day.LeaveDate <= today)
            {
                throw new InvalidOperationException("Không được phép xin nghỉ phép cho ngày hôm nay hoặc các ngày trong quá khứ.");
            }
        }

        // Validate overlapping requests
        var requestedDates = dto.Days.Select(d => d.LeaveDate).ToList();
        var minDate = requestedDates.Min();
        var maxDate = requestedDates.Max();
        var leaveSegments = await _leaveRequestRepository.GetApprovedSegmentsForEmployeesAsync(new List<Guid> { employee.Id }, minDate, maxDate);
        
        // Let's also include pending requests in the check to avoid duplicate pending requests.
        // We can just load all leave requests for the employee that are Pending or Approved.
        var allMyRequests = await _leaveRequestRepository.GetByEmployeeAsync(employee.Id);
        var activeRequests = allMyRequests.Where(r => r.Status == "Pending" || r.Status == "Approved").ToList();

        foreach (var day in dto.Days)
        {
            var conflicts = activeRequests
                .SelectMany(r => r.Segments)
                .Where(s => s.LeaveDate == day.LeaveDate)
                .ToList();

            if (conflicts.Any())
            {
                if (day.TargetShiftSegmentId == null 
                    || conflicts.Any(c => c.TargetShiftSegmentId == null || c.TargetShiftSegmentId == day.TargetShiftSegmentId))
                {
                    throw new InvalidOperationException($"Đã tồn tại đơn xin nghỉ phép trùng lặp cho ngày {day.LeaveDate}.");
                }
            }
        }

        // Calculate total requested days (8 hours = 1 day)
        var totalHours = dto.Days.Sum(d => d.HoursRequested);
        var requestedDays = totalHours / 8m;

        // Check and update balance
        var year = requestedDates.Min().Year;
        EmployeeLeaveBalance? balance = null;
        if (leaveType.DefaultAnnualDays > 0)
        {
            balance = await _leaveBalanceRepository.GetByEmployeeTypeYearAsync(employee.Id, leaveType.Id, year);
            if (balance == null)
            {
                // Auto initialize balance
                balance = new EmployeeLeaveBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EmployeeId = employee.Id,
                    LeaveTypeId = leaveType.Id,
                    Year = year,
                    TotalDays = leaveType.DefaultAnnualDays,
                    UsedDays = 0,
                    RemainingDays = leaveType.DefaultAnnualDays
                };
                await _leaveBalanceRepository.AddAsync(balance);
            }

            if (balance.RemainingDays < requestedDays)
            {
                throw new InvalidOperationException($"Số ngày phép còn lại không đủ. Còn lại: {balance.RemainingDays} ngày, Yêu cầu: {requestedDays} ngày.");
            }
        }

        var request = new LeaveRequest(tenantId, employee.Id, leaveType.Id, leaveType.Name, dto.ReasonNote, dto.AttachmentUrl);

        foreach (var d in dto.Days)
        {
            var segment = new LeaveRequestSegment(tenantId, request.Id, d.LeaveDate, d.TargetShiftSegmentId, d.HoursRequested);
            request.Segments.Add(segment);
        }

        if (!leaveType.RequiresApproval)
        {
            request.Approve(Guid.Empty, "Tự động duyệt (Không yêu cầu phê duyệt)");
            await _transaction.ExecuteAsync(async () =>
            {
                await _leaveRequestRepository.AddAsync(request);
                if (balance != null)
                {
                    balance.UsedDays += requestedDays;
                    balance.RemainingDays = balance.TotalDays - balance.UsedDays;
                    await _leaveBalanceRepository.UpdateAsync(balance);
                }
                await TriggerRecalculationAsync(tenantId, employee.Id, minDate, maxDate);
            });
        }
        else
        {
            await _leaveRequestRepository.AddAsync(request);
        }

        return MapToDto(request);
    }

    public async Task<LeaveRequestDto> CancelLeaveRequestAsync(Guid userId, Guid requestId)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");
        var employee = await _employeeRepository.GetByUserIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy nhân viên.");

        var request = await _leaveRequestRepository.GetByIdAsync(requestId) ?? throw new KeyNotFoundException("Không tìm thấy đơn xin nghỉ phép.");
        if (request.EmployeeId != employee.Id)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy đơn xin nghỉ phép này.");
        }

        var oldStatus = request.Status;
        request.Cancel();

        await _transaction.ExecuteAsync(async () =>
        {
            await _leaveRequestRepository.UpdateAsync(request);

            if (oldStatus == "Approved")
            {
                // Revert leave balance
                var year = request.Segments.Min(s => s.LeaveDate).Year;
                var balance = await _leaveBalanceRepository.GetByEmployeeTypeYearAsync(employee.Id, request.LeaveTypeId, year);
                if (balance != null)
                {
                    var requestedDays = request.Segments.Sum(s => s.HoursRequested) / 8m;
                    balance.UsedDays = Math.Max(0, balance.UsedDays - requestedDays);
                    balance.RemainingDays = balance.TotalDays - balance.UsedDays;
                    await _leaveBalanceRepository.UpdateAsync(balance);
                }

                // Trigger recalculation
                var minDate = request.Segments.Min(s => s.LeaveDate);
                var maxDate = request.Segments.Max(s => s.LeaveDate);
                await TriggerRecalculationAsync(tenantId, employee.Id, minDate, maxDate);
            }
        });

        return MapToDto(request);
    }

    public async Task<List<LeaveRequestDto>> GetMyLeaveRequestsAsync(Guid userId)
    {
        var employee = await _employeeRepository.GetByUserIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy nhân viên.");
        var list = await _leaveRequestRepository.GetByEmployeeAsync(employee.Id);
        return list.Select(MapToDto).ToList();
    }

    public async Task<List<LeaveBalanceDto>> GetMyBalancesAsync(Guid userId, int year)
    {
        var employee = await _employeeRepository.GetByUserIdAsync(userId) ?? throw new InvalidOperationException("Không tìm thấy nhân viên.");
        var balances = await _leaveBalanceRepository.GetByEmployeeAsync(employee.Id, year);
        var types = await _leaveTypeRepository.GetAllAsync();
        var typeMap = types.ToDictionary(t => t.Id);

        // Ensure all active types have a balance record initialized
        var resultList = new List<LeaveBalanceDto>();
        foreach (var type in types)
        {
            var bal = balances.FirstOrDefault(b => b.LeaveTypeId == type.Id);
            if (bal == null && type.IsActive)
            {
                bal = new EmployeeLeaveBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = employee.TenantId,
                    EmployeeId = employee.Id,
                    LeaveTypeId = type.Id,
                    Year = year,
                    TotalDays = type.DefaultAnnualDays,
                    UsedDays = 0,
                    RemainingDays = type.DefaultAnnualDays
                };
                await _leaveBalanceRepository.AddAsync(bal);
            }

            if (bal != null)
            {
                resultList.Add(new LeaveBalanceDto
                {
                    Id = bal.Id,
                    EmployeeId = bal.EmployeeId,
                    EmployeeName = employee.FullName,
                    LeaveTypeId = bal.LeaveTypeId,
                    LeaveTypeName = type.Name,
                    LeaveTypeCode = type.Code,
                    Year = bal.Year,
                    TotalDays = bal.TotalDays,
                    UsedDays = bal.UsedDays,
                    RemainingDays = bal.RemainingDays
                });
            }
        }

        return resultList;
    }

    public async Task<LeaveRequestDto> ApproveLeaveRequestAsync(Guid hrUserId, Guid requestId, ApproveLeaveRequestDto dto)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");
        var request = await _leaveRequestRepository.GetByIdAsync(requestId) ?? throw new KeyNotFoundException("Không tìm thấy đơn xin nghỉ phép.");
        
        request.Approve(hrUserId, dto.ApproverNote);

        await _transaction.ExecuteAsync(async () =>
        {
            await _leaveRequestRepository.UpdateAsync(request);

            // Deduct balance
            var year = request.Segments.Min(s => s.LeaveDate).Year;
            var balance = await _leaveBalanceRepository.GetByEmployeeTypeYearAsync(request.EmployeeId, request.LeaveTypeId, year);
            var requestedDays = request.Segments.Sum(s => s.HoursRequested) / 8m;

            if (balance == null)
            {
                var leaveType = await _leaveTypeRepository.GetByIdAsync(request.LeaveTypeId) ?? throw new InvalidOperationException("Loại nghỉ phép không tồn tại.");
                balance = new EmployeeLeaveBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EmployeeId = request.EmployeeId,
                    LeaveTypeId = request.LeaveTypeId,
                    Year = year,
                    TotalDays = leaveType.DefaultAnnualDays,
                    UsedDays = 0,
                    RemainingDays = leaveType.DefaultAnnualDays
                };
                await _leaveBalanceRepository.AddAsync(balance);
            }

            balance.UsedDays += requestedDays;
            balance.RemainingDays = balance.TotalDays - balance.UsedDays;
            await _leaveBalanceRepository.UpdateAsync(balance);

            // Trigger Recalculation
            var minDate = request.Segments.Min(s => s.LeaveDate);
            var maxDate = request.Segments.Max(s => s.LeaveDate);
            await TriggerRecalculationAsync(tenantId, request.EmployeeId, minDate, maxDate);
        });

        return MapToDto(request);
    }

    public async Task<LeaveRequestDto> RejectLeaveRequestAsync(Guid hrUserId, Guid requestId, RejectLeaveRequestDto dto)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");
        var request = await _leaveRequestRepository.GetByIdAsync(requestId) ?? throw new KeyNotFoundException("Không tìm thấy đơn xin nghỉ phép.");
        
        var oldStatus = request.Status;
        request.Reject(hrUserId, dto.RejectReason);

        await _transaction.ExecuteAsync(async () =>
        {
            await _leaveRequestRepository.UpdateAsync(request);

            if (oldStatus == "Approved")
            {
                // Revert balance
                var year = request.Segments.Min(s => s.LeaveDate).Year;
                var balance = await _leaveBalanceRepository.GetByEmployeeTypeYearAsync(request.EmployeeId, request.LeaveTypeId, year);
                if (balance != null)
                {
                    var requestedDays = request.Segments.Sum(s => s.HoursRequested) / 8m;
                    balance.UsedDays = Math.Max(0, balance.UsedDays - requestedDays);
                    balance.RemainingDays = balance.TotalDays - balance.UsedDays;
                    await _leaveBalanceRepository.UpdateAsync(balance);
                }

                // Trigger Recalculation
                var minDate = request.Segments.Min(s => s.LeaveDate);
                var maxDate = request.Segments.Max(s => s.LeaveDate);
                await TriggerRecalculationAsync(tenantId, request.EmployeeId, minDate, maxDate);
            }
        });

        return MapToDto(request);
    }

    public async Task<List<LeaveRequestDto>> GetPendingRequestsAsync()
    {
        var list = await _leaveRequestRepository.GetPendingAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<List<LeaveRequestDto>> GetAllRequestsAsync()
    {
        var list = await _leaveRequestRepository.GetAllAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<List<LeaveBalanceDto>> GetLeaveBalancesReportAsync(int year)
    {
        var balances = await _leaveBalanceRepository.GetAllAsync(year);
        var types = await _leaveTypeRepository.GetAllAsync();
        var typeMap = types.ToDictionary(t => t.Id);

        // Fetch employees to get names
        var employeeIds = balances.Select(b => b.EmployeeId).Distinct().ToList();
        var employees = await _employeeRepository.GetByIdsAsync(employeeIds);
        var employeeMap = employees.ToDictionary(e => e.Id);

        return balances.Select(bal => new LeaveBalanceDto
        {
            Id = bal.Id,
            EmployeeId = bal.EmployeeId,
            EmployeeName = employeeMap.TryGetValue(bal.EmployeeId, out var emp) ? emp.FullName : "Unknown",
            LeaveTypeId = bal.LeaveTypeId,
            LeaveTypeName = typeMap.TryGetValue(bal.LeaveTypeId, out var t) ? t.Name : "Unknown",
            LeaveTypeCode = typeMap.TryGetValue(bal.LeaveTypeId, out var t2) ? t2.Code : string.Empty,
            Year = bal.Year,
            TotalDays = bal.TotalDays,
            UsedDays = bal.UsedDays,
            RemainingDays = bal.RemainingDays
        }).ToList();
    }

    public async Task<LeaveBalanceDto> UpdateLeaveBalanceAsync(Guid balanceId, UpdateLeaveBalanceDto dto)
    {
        var balance = await _leaveBalanceRepository.GetByIdAsync(balanceId)
            ?? throw new KeyNotFoundException("Không tìm thấy bản ghi số dư nghỉ phép.");

        balance.TotalDays = dto.TotalDays;
        balance.RemainingDays = balance.TotalDays - balance.UsedDays;
        await _leaveBalanceRepository.UpdateAsync(balance);

        var employee = await _employeeRepository.GetByIdAsync(balance.EmployeeId);
        var leaveType = await _leaveTypeRepository.GetByIdAsync(balance.LeaveTypeId);

        return new LeaveBalanceDto
        {
            Id = balance.Id,
            EmployeeId = balance.EmployeeId,
            EmployeeName = employee?.FullName ?? "Unknown",
            LeaveTypeId = balance.LeaveTypeId,
            LeaveTypeName = leaveType?.Name ?? "Unknown",
            LeaveTypeCode = leaveType?.Code ?? string.Empty,
            Year = balance.Year,
            TotalDays = balance.TotalDays,
            UsedDays = balance.UsedDays,
            RemainingDays = balance.RemainingDays
        };
    }

    public async Task<List<LeaveTypeDto>> GetLeaveTypesAsync()
    {
        var types = await _leaveTypeRepository.GetAllAsync();
        return types.Select(MapTypeToDto).ToList();
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException("Tenant ID is missing.");
        var existing = await _leaveTypeRepository.GetByCodeAsync(dto.Code);
        if (existing != null)
        {
            throw new InvalidOperationException($"Mã loại nghỉ phép '{dto.Code}' đã tồn tại.");
        }

        var type = new LeaveType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            DefaultAnnualDays = dto.DefaultAnnualDays,
            RequiresApproval = dto.RequiresApproval,
            IsActive = true,
            IsDeleted = false
        };

        await _leaveTypeRepository.AddAsync(type);
        return MapTypeToDto(type);
    }

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid typeId, UpdateLeaveTypeDto dto)
    {
        var type = await _leaveTypeRepository.GetByIdAsync(typeId) ?? throw new KeyNotFoundException("Không tìm thấy loại nghỉ phép.");
        
        type.Name = dto.Name;
        type.DefaultAnnualDays = dto.DefaultAnnualDays;
        type.RequiresApproval = dto.RequiresApproval;
        type.IsActive = dto.IsActive;

        await _leaveTypeRepository.UpdateAsync(type);
        return MapTypeToDto(type);
    }

    public async Task DeleteLeaveTypeAsync(Guid typeId)
    {
        var type = await _leaveTypeRepository.GetByIdAsync(typeId) ?? throw new KeyNotFoundException("Không tìm thấy loại nghỉ phép.");
        await _leaveTypeRepository.DeleteAsync(type);
    }

    private LeaveRequestDto MapToDto(LeaveRequest r)
    {
        return new LeaveRequestDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeName = r.Employee?.FullName ?? string.Empty,
            LeaveTypeId = r.LeaveTypeId,
            LeaveTypeName = r.LeaveTypeNavigation?.Name ?? r.LeaveType,
            LeaveTypeCode = r.LeaveTypeNavigation?.Code ?? string.Empty,
            Status = r.Status,
            ReasonNote = r.ReasonNote,
            AttachmentUrl = r.AttachmentUrl,
            ApprovedByUserId = r.ApprovedByUserId,
            ApprovedAt = r.ApprovedAt,
            ApproverNote = r.ApproverNote,
            RejectedByUserId = r.RejectedByUserId,
            RejectedAt = r.RejectedAt,
            RejectReason = r.RejectReason,
            CancelledAt = r.CancelledAt,
            Segments = r.Segments.Select(s => new LeaveRequestSegmentDto
            {
                Id = s.Id,
                LeaveDate = s.LeaveDate,
                TargetShiftSegmentId = s.TargetShiftSegmentId,
                TargetShiftSegmentName = s.TargetShiftSegment != null 
                    ? $"{s.TargetShiftSegment.StartTime:hh\\:mm}-{s.TargetShiftSegment.EndTime:hh\\:mm}" 
                    : "Cả ngày",
                HoursRequested = s.HoursRequested
            }).ToList()
        };
    }

    private LeaveTypeDto MapTypeToDto(LeaveType t)
    {
        return new LeaveTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            DefaultAnnualDays = t.DefaultAnnualDays,
            RequiresApproval = t.RequiresApproval,
            IsActive = t.IsActive
        };
    }

    private async Task TriggerRecalculationAsync(Guid tenantId, Guid employeeId, DateOnly fromDate, DateOnly toDate)
    {
        var setting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId);
        var cutOffTime = setting?.DayStartCutOffTime ?? new TimeSpan(4, 0, 0);

        var utcFrom = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.FromTimeSpan(cutOffTime)), VietnamTimeZone);
        var utcTo = TimeZoneInfo.ConvertTimeToUtc(toDate.ToDateTime(TimeOnly.FromTimeSpan(cutOffTime)), VietnamTimeZone).AddDays(1);

        await _rawPunchLogRepository.MarkUnprocessedForRecalculateAsync(employeeId, utcFrom, utcTo);
    }
}
