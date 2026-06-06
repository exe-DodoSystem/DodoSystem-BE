using AutoMapper;
using Microsoft.Extensions.Logging;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.DTOs.DashboardDtos;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IEmployeeRepository _employeeRepo;
        private readonly IDailyTimesheetRepository _timesheetRepo;
        private readonly ITimesheetAppealRepository _appealRepo;
        private readonly IPayrollRepository _payrollRepo;
        private readonly IAttendanceService _attendanceService;
        private readonly IShiftPatternRepository _shiftPatternRepo;
        private readonly IHrAuthorizationService _hrAuth;
        private readonly IMapper _mapper;
        private readonly ILogger<DashboardService> _logger;
        private readonly IModuleSubscriptionService _moduleSubscription;

        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { /* Windows fallback */ }
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch { /* Linux fallback */ }
            return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
        }

        public DashboardService(
            IEmployeeRepository employeeRepo,
            IDailyTimesheetRepository timesheetRepo,
            ITimesheetAppealRepository appealRepo,
            IPayrollRepository payrollRepo,
            IAttendanceService attendanceService,
            IShiftPatternRepository shiftPatternRepo,
            IHrAuthorizationService hrAuth,
            IMapper mapper,
            ILogger<DashboardService> logger,
            IModuleSubscriptionService moduleSubscription)
        {
            _employeeRepo = employeeRepo;
            _timesheetRepo = timesheetRepo;
            _appealRepo = appealRepo;
            _payrollRepo = payrollRepo;
            _attendanceService = attendanceService;
            _shiftPatternRepo = shiftPatternRepo;
            _hrAuth = hrAuth;
            _mapper = mapper;
            _logger = logger;
            _moduleSubscription = moduleSubscription;
        }

        private static DateOnly GetVietnamWorkDate()
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var cutoff = new TimeSpan(4, 0, 0); // 04:00 AM
            var workDate = localNow.TimeOfDay < cutoff
                ? DateOnly.FromDateTime(localNow.AddDays(-1))
                : DateOnly.FromDateTime(localNow);
            return workDate;
        }

        private async Task<bool> HasModuleAsync(string moduleCode)
        {
            try
            {
                var sub = await _moduleSubscription.GetMyByModuleCodeAsync(moduleCode);
                if (sub == null) return false;
                var now = DateTime.UtcNow;
                return (string.Equals(sub.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sub.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
                       && sub.EndDate > now;
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static List<AlertItemDto> BuildAlerts(int pendingAppealsCount, int draftPayrollCount, int frequentAbsentCount, int missingOutCount)
        {
            var alerts = new List<AlertItemDto>();

            if (pendingAppealsCount > 0)
                alerts.Add(new AlertItemDto
                {
                    Type = "PendingAppeals",
                    Severity = pendingAppealsCount > 5 ? "High" : "Medium",
                    Message = $"Có {pendingAppealsCount} đơn giải trình đang chờ xử lý.",
                    Count = pendingAppealsCount
                });

            if (draftPayrollCount > 0)
                alerts.Add(new AlertItemDto
                {
                    Type = "UnpublishedPayroll",
                    Severity = "Medium",
                    Message = $"Có {draftPayrollCount} phiếu lương chưa được publish.",
                    Count = draftPayrollCount
                });

            if (frequentAbsentCount > 0)
                alerts.Add(new AlertItemDto
                {
                    Type = "FrequentAbsent",
                    Severity = frequentAbsentCount > 2 ? "High" : "Medium",
                    Message = $"Có {frequentAbsentCount} nhân viên vắng mặt từ 3 ngày trở lên trong tháng.",
                    Count = frequentAbsentCount
                });

            if (missingOutCount > 0)
                alerts.Add(new AlertItemDto
                {
                    Type = "MissingOutUnresolved",
                    Severity = missingOutCount > 2 ? "High" : "Medium",
                    Message = $"Có {missingOutCount} nhân viên có ngày thiếu chấm ra chưa giải trình.",
                    Count = missingOutCount
                });

            return alerts;
        }

        public async Task<AdminDashboardDto> GetAdminDashboardAsync(Guid tenantId, int month, int year)
        {
            var workDate = GetVietnamWorkDate();

            var hasAttendance = await HasModuleAsync("ATTENDANCE");
            var hasPayroll = await HasModuleAsync("PAYROLL");

            var employees = await _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId);
            
            var todayTimesheets = hasAttendance 
                ? await _timesheetRepo.GetByTenantDateAsync(tenantId, workDate)
                : new List<DailyTimesheet>();

            var monthTimesheets = hasAttendance
                ? await _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year)
                : new List<DailyTimesheet>();

            var pendingAppeals = hasAttendance
                ? await _appealRepo.GetPendingAsync(tenantId)
                : new List<TimesheetAppeal>();

            var payrolls = hasPayroll
                ? await _payrollRepo.GetByTenantMonthAsync(tenantId, month, year)
                : new List<Payroll>();

            var totalEmployees = employees.Count;

            var employeesByDepartment = employees
                .Where(e => e.DepartmentId.HasValue && e.Department != null)
                .GroupBy(e => e.DepartmentId!.Value)
                .Select(g => new DepartmentEmployeeCountDto
                {
                    DepartmentId = g.Key,
                    DepartmentName = g.First().Department?.Name ?? "Phòng ban khác",
                    Count = g.Count()
                })
                .OrderBy(d => d.DepartmentName)
                .ToList();

            TodayAttendanceSummaryDto? todayAttendance = null;
            if (hasAttendance)
            {
                todayAttendance = new TodayAttendanceSummaryDto
                {
                    WorkDate = workDate,
                    CheckedIn = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                    Absent = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                    Late = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                    MissingOut = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceMissingOut),
                    OnLeave = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceOnLeave),
                    TotalExpected = todayTimesheets.Count
                };
            }

            MonthlyAttendanceStatsDto? monthlyStats = null;
            if (hasAttendance)
            {
                monthlyStats = new MonthlyAttendanceStatsDto
                {
                    Month = month,
                    Year = year,
                    TotalWorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                    TotalAbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                    TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                    TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes),
                    TotalEmployeeRecords = monthTimesheets.Count
                };
            }

            PayrollSummaryDto? payrollSummary = null;
            if (hasPayroll)
            {
                payrollSummary = new PayrollSummaryDto
                {
                    Month = month,
                    Year = year,
                    DraftCount = payrolls.Count(p => p.Status == PayrollStatus.Draft),
                    PublishedCount = payrolls.Count(p => p.Status == PayrollStatus.Published),
                    PaidCount = payrolls.Count(p => p.Status == PayrollStatus.Paid),
                    TotalNetSalary = payrolls.Sum(p => p.NetSalary),
                    TotalPaidSalary = payrolls.Where(p => p.Status == PayrollStatus.Paid).Sum(p => p.NetSalary)
                };
            }

            int? pendingAppealsCount = null;
            if (hasAttendance)
            {
                pendingAppealsCount = pendingAppeals.Count;
            }

            int frequentAbsentCount = 0;
            int missingOutCount = 0;
            if (hasAttendance)
            {
                frequentAbsentCount = monthTimesheets
                    .Where(t => t.Status == StatusEnum.AttendanceAbsent)
                    .GroupBy(t => t.EmployeeId)
                    .Count(g => g.Count() >= 3);

                var missingOutEmpIds = monthTimesheets
                    .Where(t => t.Status == StatusEnum.AttendanceMissingOut)
                    .Select(t => t.EmployeeId)
                    .ToHashSet();
                var appealedEmpIds = pendingAppeals
                    .Select(a => a.EmployeeId)
                    .ToHashSet();
                missingOutCount = missingOutEmpIds.Except(appealedEmpIds).Count();
            }

            var alerts = BuildAlerts(
                hasAttendance ? (pendingAppealsCount ?? 0) : 0, 
                hasPayroll && payrollSummary != null ? payrollSummary.DraftCount : 0, 
                hasAttendance ? frequentAbsentCount : 0, 
                hasAttendance ? missingOutCount : 0);

            var availableModules = new List<string> { "HR" };
            if (hasAttendance) availableModules.Add("ATTENDANCE");
            if (hasPayroll) availableModules.Add("PAYROLL");

            return new AdminDashboardDto
            {
                TotalEmployees = totalEmployees,
                EmployeesByDepartment = employeesByDepartment,
                TodayAttendance = todayAttendance,
                MonthlyStats = monthlyStats,
                PayrollSummary = payrollSummary,
                PendingAppealsCount = pendingAppealsCount,
                Alerts = alerts,
                AvailableModules = availableModules
            };
        }

        public async Task<ManagerDashboardDto> GetManagerDashboardAsync(Guid tenantId, Guid userId, int month, int year)
        {
            var workDate = GetVietnamWorkDate();

            var departmentIds = await _hrAuth.GetAccessibleDepartmentIdsAsync();
            if (departmentIds != null && !departmentIds.Any())
            {
                return new ManagerDashboardDto();
            }

            var allEmployees = await _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId);
            var employees = departmentIds == null
                ? allEmployees
                : allEmployees.Where(e => e.DepartmentId.HasValue && departmentIds.Contains(e.DepartmentId.Value)).ToList();

            var empIds = employees.Select(e => e.Id).ToHashSet();
            if (empIds.Count == 0)
            {
                return new ManagerDashboardDto();
            }

            var hasAttendance = await HasModuleAsync("ATTENDANCE");
            var hasPayroll = await HasModuleAsync("PAYROLL");

            var todayTimesheetsRaw = hasAttendance
                ? await _timesheetRepo.GetByTenantDateAsync(tenantId, workDate)
                : new List<DailyTimesheet>();

            var monthTimesheetsRaw = hasAttendance
                ? await _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year)
                : new List<DailyTimesheet>();

            var pendingAppealsRaw = hasAttendance
                ? await _appealRepo.GetPendingAsync(tenantId)
                : new List<TimesheetAppeal>();

            var payrollsRaw = hasPayroll
                ? await _payrollRepo.GetByTenantMonthAsync(tenantId, month, year)
                : new List<Payroll>();

            var todayTimesheets = hasAttendance 
                ? todayTimesheetsRaw.Where(t => empIds.Contains(t.EmployeeId)).ToList()
                : new List<DailyTimesheet>();
            var monthTimesheets = hasAttendance
                ? monthTimesheetsRaw.Where(t => empIds.Contains(t.EmployeeId)).ToList()
                : new List<DailyTimesheet>();
            var pendingAppeals = hasAttendance
                ? pendingAppealsRaw.Where(a => empIds.Contains(a.EmployeeId)).ToList()
                : new List<TimesheetAppeal>();
            var payrolls = hasPayroll
                ? payrollsRaw.Where(p => empIds.Contains(p.EmployeeId)).ToList()
                : new List<Payroll>();

            var deptEmployeeCount = employees.Count;

            var employeesByDepartment = employees
                .Where(e => e.DepartmentId.HasValue && e.Department != null)
                .GroupBy(e => e.DepartmentId!.Value)
                .Select(g => new DepartmentEmployeeCountDto
                {
                    DepartmentId = g.Key,
                    DepartmentName = g.First().Department?.Name ?? "Phòng ban khác",
                    Count = g.Count()
                })
                .OrderBy(d => d.DepartmentName)
                .ToList();

            TodayAttendanceSummaryDto? deptTodayAttendance = null;
            if (hasAttendance)
            {
                deptTodayAttendance = new TodayAttendanceSummaryDto
                {
                    WorkDate = workDate,
                    CheckedIn = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                    Absent = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                    Late = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                    MissingOut = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceMissingOut),
                    OnLeave = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceOnLeave),
                    TotalExpected = todayTimesheets.Count
                };
            }

            MonthlyAttendanceStatsDto? deptMonthlyStats = null;
            if (hasAttendance)
            {
                deptMonthlyStats = new MonthlyAttendanceStatsDto
                {
                    Month = month,
                    Year = year,
                    TotalWorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                    TotalAbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                    TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                    TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes),
                    TotalEmployeeRecords = monthTimesheets.Count
                };
            }

            int? draftPayrollCount = null;
            if (hasPayroll)
            {
                draftPayrollCount = payrolls.Count(p => p.Status == PayrollStatus.Draft);
            }

            int? deptPendingAppealsCount = null;
            if (hasAttendance)
            {
                deptPendingAppealsCount = pendingAppeals.Count;
            }

            int frequentAbsentCount = 0;
            int missingOutCount = 0;
            if (hasAttendance)
            {
                frequentAbsentCount = monthTimesheets
                    .Where(t => t.Status == StatusEnum.AttendanceAbsent)
                    .GroupBy(t => t.EmployeeId)
                    .Count(g => g.Count() >= 3);

                var missingOutEmpIds = monthTimesheets
                    .Where(t => t.Status == StatusEnum.AttendanceMissingOut)
                    .Select(t => t.EmployeeId)
                    .ToHashSet();
                var appealedEmpIds = pendingAppeals
                    .Select(a => a.EmployeeId)
                    .ToHashSet();
                missingOutCount = missingOutEmpIds.Except(appealedEmpIds).Count();
            }

            var alerts = BuildAlerts(
                hasAttendance ? (deptPendingAppealsCount ?? 0) : 0, 
                hasPayroll ? (draftPayrollCount ?? 0) : 0, 
                hasAttendance ? frequentAbsentCount : 0, 
                hasAttendance ? missingOutCount : 0);

            var availableModules = new List<string> { "HR" };
            if (hasAttendance) availableModules.Add("ATTENDANCE");
            if (hasPayroll) availableModules.Add("PAYROLL");

            return new ManagerDashboardDto
            {
                DeptEmployeeCount = deptEmployeeCount,
                EmployeesByDepartment = employeesByDepartment,
                DeptTodayAttendance = deptTodayAttendance,
                DeptMonthlyStats = deptMonthlyStats,
                DraftPayrollCount = draftPayrollCount,
                DeptPendingAppealsCount = deptPendingAppealsCount,
                Alerts = alerts,
                AvailableModules = availableModules
            };
        }

        public async Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(Guid userId, int month, int year)
        {
            var workDate = GetVietnamWorkDate();

            var employee = await _employeeRepo.GetByUserIdAsync(userId)
                ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ nhân sự cho tài khoản này.");

            var hasAttendance = await HasModuleAsync("ATTENDANCE");
            var hasPayroll = await HasModuleAsync("PAYROLL");

            var todayStatus = hasAttendance
                ? await _attendanceService.GetMyTodayStatusAsync(userId)
                : null;

            var monthTimesheets = hasAttendance
                ? await _timesheetRepo.GetByEmployeeMonthAsync(employee.Id, month, year)
                : new List<DailyTimesheet>();

            var (esp, definition) = hasAttendance
                ? await _shiftPatternRepo.GetActivePatternDetailsAsync(employee.Id, workDate)
                : (null, null);

            var payrolls = hasPayroll
                ? await _payrollRepo.GetByEmployeeMonthAsync(employee.Id, employee.TenantId, month, year)
                : new List<Payroll>();

            var appeals = hasAttendance
                ? await _appealRepo.GetByEmployeeAsync(employee.Id)
                : new List<TimesheetAppeal>();

            MyMonthSummaryDto? myMonthSummary = null;
            if (hasAttendance)
            {
                myMonthSummary = new MyMonthSummaryDto
                {
                    Month = month,
                    Year = year,
                    WorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                    AbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                    LateDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                    TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                    TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes)
                };
            }

            CurrentShiftDto? myCurrentShift = null;
            if (hasAttendance && esp != null && definition != null && definition.CycleLengthDays > 0)
            {
                var dayOffset = workDate.DayNumber - esp.EffectiveStartDate.DayNumber;
                var dayIndex = dayOffset % definition.CycleLengthDays;
                if (dayIndex < 0) dayIndex += definition.CycleLengthDays;

                var patternDay = definition.Days.FirstOrDefault(d => d.DayIndex == dayIndex);
                if (patternDay?.ScheduledShiftId != null)
                {
                    var shift = await _shiftPatternRepo.GetShiftWithSegmentsAsync(patternDay.ScheduledShiftId.Value);
                    if (shift != null)
                    {
                        var sortedSegments = shift.Segments.OrderBy(s => s.StartDayOffset).ThenBy(s => s.StartTime).ToList();
                        var firstSeg = sortedSegments.FirstOrDefault();
                        var lastSeg = sortedSegments.LastOrDefault();

                        myCurrentShift = new CurrentShiftDto
                        {
                            ShiftPatternId = definition.Id,
                            ShiftName = shift.Name,
                            StartTime = firstSeg != null ? TimeOnly.FromTimeSpan(firstSeg.StartTime) : null,
                            EndTime = lastSeg != null ? TimeOnly.FromTimeSpan(lastSeg.EndTime) : null
                        };
                    }
                }
            }

            PayrollDto? myLatestPayroll = null;
            if (hasPayroll)
            {
                var payroll = payrolls.FirstOrDefault();
                if (payroll != null && (payroll.Status == PayrollStatus.Published || payroll.Status == PayrollStatus.Paid))
                {
                    myLatestPayroll = _mapper.Map<PayrollDto>(payroll);
                }
            }

            int? myPendingAppealsCount = null;
            if (hasAttendance)
            {
                myPendingAppealsCount = appeals.Count(a => a.Status == StatusEnum.ApprovalPending);
            }

            var availableModules = new List<string> { "HR" };
            if (hasAttendance) availableModules.Add("ATTENDANCE");
            if (hasPayroll) availableModules.Add("PAYROLL");

            return new EmployeeDashboardDto
            {
                MyTodayStatus = todayStatus,
                MyMonthSummary = myMonthSummary,
                MyCurrentShift = myCurrentShift,
                MyLatestPayroll = myLatestPayroll,
                MyPendingAppealsCount = myPendingAppealsCount,
                AvailableModules = availableModules
            };
        }
    }
}
