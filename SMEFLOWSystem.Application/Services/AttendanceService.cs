using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Helpers;

namespace SMEFLOWSystem.Application.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IRawPunchLogRepository _punchLogRepo;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDailyTimesheetRepository _dailyTimesheetRepository;
        private readonly IAttendanceSettingRepository _attendanceSettingRepository;
        private readonly ICurrentTenantService _currentTenantService;
        private readonly ITimesheetAppealRepository _appealRepository;

        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { /* Windows không có */ }
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch { /* Linux không có */ }
            return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
        }

        public AttendanceService(
            IRawPunchLogRepository punchLogRepo, 
            IEmployeeRepository employeeRepository,
            IDailyTimesheetRepository dailyTimesheetRepository,
            IAttendanceSettingRepository attendanceSettingRepository,
            ICurrentTenantService currentTenantService,
            ITimesheetAppealRepository appealRepository)
        {
            _punchLogRepo = punchLogRepo;
            _employeeRepository = employeeRepository;
            _dailyTimesheetRepository = dailyTimesheetRepository;
            _attendanceSettingRepository = attendanceSettingRepository;
            _currentTenantService = currentTenantService;
            _appealRepository = appealRepository;
        }

        public async Task<RawPunchLogDto> SubmitPunchAsync(Guid userId, SubmitPunchRequestDto request)
        {
            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found for current user.");
            }

            if (request.IsMockLocation)
            {
                throw new InvalidOperationException("FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí. Vui lòng tắt Fake GPS!");
            }

            var tenantId = _currentTenantService.TenantId ?? employee.TenantId;
            var attendanceSetting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId);

            // Geofencing Validation
            if (attendanceSetting != null && attendanceSetting.Latitude.HasValue && attendanceSetting.Longitude.HasValue)
            {
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    throw new InvalidOperationException("BatBuocGPS: Vui lòng bật định vị GPS để chấm công.");
                }

                var distance = GeoHelper.DistanceInMeters(
                    request.Latitude.Value, request.Longitude.Value,
                    attendanceSetting.Latitude.Value, attendanceSetting.Longitude.Value);

                if (distance > attendanceSetting.CheckInRadiusMeters)
                {
                    throw new InvalidOperationException($"NgoaiVung: Bạn đang ở ngoài vùng chấm công cho phép (Cách {Math.Round(distance)}m). Bán kính cho phép là {attendanceSetting.CheckInRadiusMeters}m.");
                }
            }

            var punch = new RawPunchLog()
            {
                EmployeeId = employee.Id,
                Timestamp = DateTime.UtcNow,
                DeviceId = request.DeviceId,
                PunchType = request.PunchType ?? "Auto",
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                SelfieUrl = request.SelfieUrl,
                IsProcessed = false 
            };

            await _punchLogRepo.AddAsync(punch);

            return new RawPunchLogDto
            {
                Id = punch.Id,
                EmployeeId = punch.EmployeeId,
                Timestamp = punch.Timestamp,
                DeviceId = punch.DeviceId,
                IsProcessed = punch.IsProcessed,
                PunchType = punch.PunchType
            };
        }

        public async Task<TodayAttendanceDto> GetMyTodayStatusAsync(Guid userId)
        {
            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found for current user.");
            }

            var tenantId = _currentTenantService.TenantId ?? employee.TenantId;
            var attendanceSetting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId);
            
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var cutOffTime = attendanceSetting?.DayStartCutOffTime ?? new TimeSpan(12, 1, 0);
            
            var workDate = localTime.TimeOfDay < cutOffTime
                ? DateOnly.FromDateTime(localTime.AddDays(-1))
                : DateOnly.FromDateTime(localTime);

            var timesheet = await _dailyTimesheetRepository.GetByEmployeeDateAsync(employee.Id, workDate);
            
            var result = new TodayAttendanceDto
            {
                HasCheckedIn = false,
                HasCheckedOut = false
            };

            if (timesheet != null && timesheet.Segments.Any())
            {
                var firstSegment = timesheet.Segments.OrderBy(s => s.ActualCheckIn).FirstOrDefault(s => s.ActualCheckIn.HasValue);
                var lastSegment = timesheet.Segments.OrderByDescending(s => s.ActualCheckOut).FirstOrDefault();

                if (firstSegment?.ActualCheckIn != null)
                {
                    result.HasCheckedIn = true;
                    result.CheckInTime = firstSegment.ActualCheckIn;
                    result.CheckInSelfieUrl = firstSegment.CheckInSelfieUrl;
                }

                if (lastSegment?.ActualCheckOut != null)
                {
                    result.HasCheckedOut = true;
                    result.CheckOutTime = lastSegment.ActualCheckOut;
                }

                result.LateMinutes = timesheet.TotalLateMinutes;
                result.EarlyLeaveMinutes = timesheet.TotalEarlyLeaveMinutes;

                if (timesheet.SystemAnomalyFlag.Contains("MissingOut"))
                {
                    result.Status = StatusEnum.AttendanceMissingOut;
                }
                else if (timesheet.TotalLateMinutes > 0)
                {
                    result.Status = StatusEnum.AttendanceLate;
                }
                else
                {
                    result.Status = StatusEnum.AttendancePresent;
                }
            }
            else
            {
                // Nếu chưa có timesheet do job chưa chạy, ta query raw log để show tạm
                // Sẽ query RawPunchLog có Timestamp trong workDate range này
            }

            return result;
        }

        public async Task<List<MyAttendanceHistoryItemDto>> GetMyHistoryAsync(Guid userId, int month, int year)
        {
            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found for current user.");
            }

            var timesheets = await _dailyTimesheetRepository.GetByEmployeeMonthAsync(employee.Id, month, year);
            var results = new List<MyAttendanceHistoryItemDto>();

            foreach(var ts in timesheets)
            {
                var dto = new MyAttendanceHistoryItemDto
                {
                    WorkDate = ts.WorkDate,
                    StandardWorkingHours = ts.StandardWorkingHours,
                    TotalActualWorkedMinutes = ts.TotalActualWorkedMinutes,
                    TotalLateMinutes = ts.TotalLateMinutes,
                    TotalEarlyLeaveMinutes = ts.TotalEarlyLeaveMinutes,
                    SystemAnomalyFlag = ts.SystemAnomalyFlag,
                    IsManuallyAdjusted = ts.IsManuallyAdjusted,
                    Segments = ts.Segments.Select(s => new MyAttendanceSegmentDto
                    {
                        ActualCheckIn = s.ActualCheckIn,
                        ActualCheckOut = s.ActualCheckOut,
                        LateMinutes = s.LateMinutes,
                        EarlyLeaveMinutes = s.EarlyLeaveMinutes,
                        Status = s.Status
                    }).ToList()
                };
                results.Add(dto);
            }

            return results;
        }

        public async Task<RawPunchLogDto> ManualPunchAsync(ManualPunchRequestDto request)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
            if (employee == null)
                throw new InvalidOperationException("Employee not found.");

            var punch = new RawPunchLog()
            {
                EmployeeId = request.EmployeeId,
                Timestamp = request.Timestamp, // Giờ UTC mà HR chọn
                DeviceId = "HR_Manual", // Đánh dấu đây là log do HR thêm tay để phân quyền audit
                PunchType = request.PunchType,
                IsProcessed = false,
                Latitude = null,
                Longitude = null,
            };

            await _punchLogRepo.AddAsync(punch);

            return new RawPunchLogDto
            {
                Id = punch.Id,
                EmployeeId = punch.EmployeeId,
                Timestamp = punch.Timestamp,
                DeviceId = punch.DeviceId,
                IsProcessed = punch.IsProcessed,
                PunchType = punch.PunchType
            };
        }

        public async Task RecalculateAttendanceAsync(Guid employeeId, DateOnly fromDate, DateOnly toDate)
        {
            // Set IsProcessed = false cho toàn bộ log trong dải thời gian này để Background job chạy lại
            await _punchLogRepo.MarkUnprocessedForRecalculateAsync(employeeId, fromDate.ToDateTime(TimeOnly.MinValue), toDate.ToDateTime(TimeOnly.MaxValue));
        }
        public async Task<TimesheetAppealDto> SubmitAppealAsync(Guid userId, SubmitAppealRequestDto request)
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null) throw new Exception("Employee not found");

            var appeal = new TimesheetAppeal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                EmployeeId = employee.Id,
                WorkDate = request.WorkDate,
                AppealType = request.AppealType,
                RequestedCheckIn = request.RequestedCheckIn,
                RequestedCheckOut = request.RequestedCheckOut,
                Reason = request.Reason,
                AttachmentUrl = request.AttachmentUrl,
                Status = "PendingApproval"
            };

            await _appealRepository.AddAsync(appeal);

            return new TimesheetAppealDto
            {
                Id = appeal.Id,
                EmployeeId = appeal.EmployeeId,
                WorkDate = appeal.WorkDate,
                AppealType = appeal.AppealType,
                RequestedCheckIn = appeal.RequestedCheckIn,
                RequestedCheckOut = appeal.RequestedCheckOut,
                Reason = appeal.Reason,
                AttachmentUrl = appeal.AttachmentUrl,
                Status = appeal.Status,
                ApprovedAt = appeal.ApprovedAt,
                RejectReason = appeal.RejectReason
            };
        }

        public async Task<List<TimesheetAppealDto>> GetMyAppealsAsync(Guid userId)
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null) return new List<TimesheetAppealDto>();

            var appeals = await _appealRepository.GetByEmployeeAsync(employee.Id);
            // Optionally, we could verify they belong to current tenant just to be safe
            appeals = appeals.Where(a => a.TenantId == tenantId.Value).ToList();
            
            return appeals.Select(appeal => new TimesheetAppealDto
            {
                Id = appeal.Id,
                EmployeeId = appeal.EmployeeId,
                WorkDate = appeal.WorkDate,
                AppealType = appeal.AppealType,
                RequestedCheckIn = appeal.RequestedCheckIn,
                RequestedCheckOut = appeal.RequestedCheckOut,
                Reason = appeal.Reason,
                AttachmentUrl = appeal.AttachmentUrl,
                Status = appeal.Status,
                ApprovedAt = appeal.ApprovedAt,
                RejectReason = appeal.RejectReason
            }).OrderByDescending(x => x.WorkDate).ToList();
        }

        public async Task<TimesheetAppealDto> ProcessAppealAsync(Guid hrUserId, Guid appealId, ApproveAppealRequestDto request)
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var appeal = await _appealRepository.GetByIdAsync(appealId);
            if (appeal == null || appeal.TenantId != tenantId.Value)
                throw new Exception("Appeal not found");

            if (appeal.Status != "PendingApproval")
                throw new Exception("This appeal has already been processed.");

            var hrUser = await _employeeRepository.GetByUserIdAsync(hrUserId);
            if (hrUser == null) throw new Exception("HR Employee record not found.");

            if (request.IsApproved)
            {
                appeal.Status = "Approved";
                appeal.ApprovedBy = hrUser.Id;
                appeal.ApprovedAt = DateTime.UtcNow;

                // Create HR_Manual punches
                if (appeal.AppealType == "In" || appeal.AppealType == "Both")
                {
                    if (appeal.RequestedCheckIn.HasValue)
                    {
                        await _punchLogRepo.AddAsync(new RawPunchLog
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId.Value,
                            EmployeeId = appeal.EmployeeId,
                            Timestamp = appeal.RequestedCheckIn.Value,
                            PunchType = "I",
                            DeviceId = "HR_Manual"
                        });
                    }
                }

                if (appeal.AppealType == "Out" || appeal.AppealType == "Both")
                {
                    if (appeal.RequestedCheckOut.HasValue)
                    {
                        await _punchLogRepo.AddAsync(new RawPunchLog
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId.Value,
                            EmployeeId = appeal.EmployeeId,
                            Timestamp = appeal.RequestedCheckOut.Value,
                            PunchType = "O",
                            DeviceId = "HR_Manual"
                        });
                    }
                }

                // Force recalculation for that day
                await _punchLogRepo.MarkUnprocessedForRecalculateAsync(
                    appeal.EmployeeId, 
                    appeal.WorkDate.ToDateTime(TimeOnly.MinValue), 
                    appeal.WorkDate.ToDateTime(TimeOnly.MaxValue)
                );
            }
            else
            {
                appeal.Status = "Rejected";
                appeal.ApprovedBy = hrUser.Id;
                appeal.ApprovedAt = DateTime.UtcNow;
                appeal.RejectReason = request.RejectReason;
            }

            await _appealRepository.UpdateAsync(appeal);

            return new TimesheetAppealDto
            {
                Id = appeal.Id,
                EmployeeId = appeal.EmployeeId,
                WorkDate = appeal.WorkDate,
                AppealType = appeal.AppealType,
                RequestedCheckIn = appeal.RequestedCheckIn,
                RequestedCheckOut = appeal.RequestedCheckOut,
                Reason = appeal.Reason,
                AttachmentUrl = appeal.AttachmentUrl,
                Status = appeal.Status,
                ApprovedAt = appeal.ApprovedAt,
                RejectReason = appeal.RejectReason
            };
        }

        public async Task<List<TimesheetAppealDto>> GetPendingAppealsAsync()
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var appeals = await _appealRepository.GetPendingAsync(tenantId.Value);

            return appeals.Select(appeal => new TimesheetAppealDto
            {
                Id = appeal.Id,
                EmployeeId = appeal.EmployeeId,
                WorkDate = appeal.WorkDate,
                AppealType = appeal.AppealType,
                RequestedCheckIn = appeal.RequestedCheckIn,
                RequestedCheckOut = appeal.RequestedCheckOut,
                Reason = appeal.Reason,
                AttachmentUrl = appeal.AttachmentUrl,
                Status = appeal.Status,
                ApprovedAt = appeal.ApprovedAt,
                RejectReason = appeal.RejectReason
            }).OrderBy(x => x.WorkDate).ToList();
        }

        public async Task<AttendanceSettingDto> GetSettingsAsync()
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var setting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId.Value);

            if (setting == null)
            {
                // Return default setting if not configured yet
                return new AttendanceSettingDto
                {
                    TenantId = tenantId.Value,
                    CheckInRadiusMeters = 100,
                    DayStartCutOffTime = new TimeSpan(12, 1, 0),
                    LateThresholdMinutes = 10,
                    EarlyLeaveThresholdMinutes = 10,
                    MinimumOTMinutes = 30,
                    OTBlockMinutes = 30
                };
            }

            return new AttendanceSettingDto
            {
                TenantId = setting.TenantId,
                Latitude = setting.Latitude,
                Longitude = setting.Longitude,
                CheckInRadiusMeters = setting.CheckInRadiusMeters,
                WorkStartTime = setting.WorkStartTime?.ToTimeSpan(),
                WorkEndTime = setting.WorkEndTime?.ToTimeSpan(),
                DayStartCutOffTime = setting.DayStartCutOffTime,
                LateThresholdMinutes = setting.LateThresholdMinutes,
                EarlyLeaveThresholdMinutes = setting.EarlyLeaveThresholdMinutes,
                MinimumOTMinutes = setting.MinimumOTMinutes,
                OTBlockMinutes = setting.OTBlockMinutes
            };
        }

        public async Task<AttendanceSettingDto> UpdateSettingsAsync(UpdateAttendanceSettingRequestDto request)
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var setting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId.Value);

            if (setting == null)
            {
                setting = new SMEFLOWSystem.Core.Entities.TenantAttendanceSetting
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value
                };
            }

            setting.Latitude = request.Latitude;
            setting.Longitude = request.Longitude;
            setting.CheckInRadiusMeters = request.CheckInRadiusMeters;
            setting.WorkStartTime = request.WorkStartTime.HasValue ? TimeOnly.FromTimeSpan(request.WorkStartTime.Value) : null;
            setting.WorkEndTime = request.WorkEndTime.HasValue ? TimeOnly.FromTimeSpan(request.WorkEndTime.Value) : null;
            setting.DayStartCutOffTime = request.DayStartCutOffTime;
            setting.LateThresholdMinutes = request.LateThresholdMinutes;
            setting.EarlyLeaveThresholdMinutes = request.EarlyLeaveThresholdMinutes;
            setting.MinimumOTMinutes = request.MinimumOTMinutes;
            setting.OTBlockMinutes = request.OTBlockMinutes;
            setting.UpdatedAt = DateTime.UtcNow;

            await _attendanceSettingRepository.UpsertAsync(setting);

            return await GetSettingsAsync();
        }

        public async Task<List<HRMonthlyReportItemDto>> GetHRMonthlyReportAsync(int month, int year)
        {
            var tenantId = _currentTenantService.TenantId;
            if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID is missing.");

            var timesheets = await _dailyTimesheetRepository.GetByTenantMonthAsync(tenantId.Value, month, year);

            var report = timesheets.GroupBy(t => new { t.EmployeeId, t.Employee?.FullName, t.Employee?.EmployeeCode })
                .Select(g => new HRMonthlyReportItemDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = g.Key.FullName ?? "Unknown",
                    EmployeeCode = g.Key.EmployeeCode ?? "Unknown",
                    Month = month,
                    Year = year,
                    TotalWorkDays = g.Count(x => x.Status == StatusEnum.AttendanceNormal || x.Status == StatusEnum.AttendanceMissingOut),
                    TotalActualHours = g.Sum(x => x.ActualWorkHours),
                    TotalOTHours = g.Sum(x => x.OTHours),
                    TotalLateMinutes = g.Sum(x => x.LateMinutes),
                    TotalEarlyLeaveMinutes = g.Sum(x => x.EarlyLeaveMinutes),
                    MissingPunches = g.Count(x => x.Status == StatusEnum.AttendanceMissingOut || x.Status == StatusEnum.AttendanceAbsent)
                })
                .OrderBy(x => x.EmployeeName)
                .ToList();

            return report;
        }
    }
}
