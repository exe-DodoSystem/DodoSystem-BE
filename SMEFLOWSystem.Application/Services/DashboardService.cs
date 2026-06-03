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
            ILogger<DashboardService> logger)
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

        private List<AlertItemDto> BuildAlerts(int pendingAppealsCount, int draftPayrollCount, int highLateCount, int missingOutCount)
        {
            return new List<AlertItemDto>
            {
                new AlertItemDto
                {
                    Type = "PendingAppeals",
                    Severity = pendingAppealsCount > 5 ? "High" : (pendingAppealsCount > 0 ? "Medium" : "Low"),
                    Message = $"Có {pendingAppealsCount} đơn giải trình đang chờ xử lý.",
                    Count = pendingAppealsCount
                },
                new AlertItemDto
                {
                    Type = "UnpublishedPayroll",
                    Severity = draftPayrollCount > 0 ? "Medium" : "Low",
                    Message = $"Có {draftPayrollCount} nhân viên đang ở trạng thái lương Nháp.",
                    Count = draftPayrollCount
                },
                new AlertItemDto
                {
                    Type = "FrequentAbsent",
                    Severity = highLateCount > 2 ? "High" : (highLateCount > 0 ? "Medium" : "Low"),
                    Message = $"Có {highLateCount} nhân viên đi trễ từ 3 lần trở lên trong tháng.",
                    Count = highLateCount
                },
                new AlertItemDto
                {
                    Type = "MissingOutUnresolved",
                    Severity = missingOutCount > 2 ? "High" : (missingOutCount > 0 ? "Medium" : "Low"),
                    Message = $"Có {missingOutCount} nhân viên có ngày chưa checkout trong tháng.",
                    Count = missingOutCount
                }
            };
        }

        public async Task<AdminDashboardDto> GetAdminDashboardAsync(Guid tenantId, int month, int year)
        {
            var workDate = GetVietnamWorkDate();

            var employeesTask = _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId);
            var todayTimesheetsTask = _timesheetRepo.GetByTenantDateAsync(tenantId, workDate);
            var monthTimesheetsTask = _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year);
            var pendingAppealsTask = _appealRepo.GetPendingAsync(tenantId);
            var payrollsTask = _payrollRepo.GetByTenantMonthAsync(tenantId, month, year);

            await Task.WhenAll(employeesTask, todayTimesheetsTask, monthTimesheetsTask, pendingAppealsTask, payrollsTask);

            var employees = await employeesTask;
            var todayTimesheets = await todayTimesheetsTask;
            var monthTimesheets = await monthTimesheetsTask;
            var pendingAppeals = await pendingAppealsTask;
            var payrolls = await payrollsTask;

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

            var todayAttendance = new TodayAttendanceSummaryDto
            {
                WorkDate = workDate,
                CheckedIn = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                Absent = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                Late = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                MissingOut = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceMissingOut),
                OnLeave = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceOnLeave),
                TotalExpected = todayTimesheets.Count
            };

            var monthlyStats = new MonthlyAttendanceStatsDto
            {
                Month = month,
                Year = year,
                TotalWorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                TotalAbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes),
                TotalEmployeeRecords = monthTimesheets.Count
            };

            var payrollSummary = new PayrollSummaryDto
            {
                Month = month,
                Year = year,
                DraftCount = payrolls.Count(p => p.Status == PayrollStatus.Draft),
                PublishedCount = payrolls.Count(p => p.Status == PayrollStatus.Published),
                PaidCount = payrolls.Count(p => p.Status == PayrollStatus.Paid),
                TotalNetSalary = payrolls.Sum(p => p.NetSalary),
                TotalPaidSalary = payrolls.Where(p => p.Status == PayrollStatus.Paid).Sum(p => p.NetSalary)
            };

            var pendingAppealsCount = pendingAppeals.Count;

            var highLateCount = monthTimesheets
                .Where(t => t.Status == StatusEnum.AttendanceLate)
                .GroupBy(t => t.EmployeeId)
                .Count(g => g.Count() >= 3);

            var missingOutCount = monthTimesheets
                .Where(t => t.Status == StatusEnum.AttendanceMissingOut)
                .Select(t => t.EmployeeId)
                .Distinct()
                .Count();

            var alerts = BuildAlerts(pendingAppealsCount, payrollSummary.DraftCount, highLateCount, missingOutCount);

            return new AdminDashboardDto
            {
                TotalEmployees = totalEmployees,
                EmployeesByDepartment = employeesByDepartment,
                TodayAttendance = todayAttendance,
                MonthlyStats = monthlyStats,
                PayrollSummary = payrollSummary,
                PendingAppealsCount = pendingAppealsCount,
                Alerts = alerts
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

            var todayTimesheetsTask = _timesheetRepo.GetByTenantDateAsync(tenantId, workDate);
            var monthTimesheetsTask = _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year);
            var pendingAppealsTask = _appealRepo.GetPendingAsync(tenantId);
            var payrollsTask = _payrollRepo.GetByTenantMonthAsync(tenantId, month, year);

            await Task.WhenAll(todayTimesheetsTask, monthTimesheetsTask, pendingAppealsTask, payrollsTask);

            var todayTimesheets = (await todayTimesheetsTask).Where(t => empIds.Contains(t.EmployeeId)).ToList();
            var monthTimesheets = (await monthTimesheetsTask).Where(t => empIds.Contains(t.EmployeeId)).ToList();
            var pendingAppeals = (await pendingAppealsTask).Where(a => empIds.Contains(a.EmployeeId)).ToList();
            var payrolls = (await payrollsTask).Where(p => empIds.Contains(p.EmployeeId)).ToList();

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

            var deptTodayAttendance = new TodayAttendanceSummaryDto
            {
                WorkDate = workDate,
                CheckedIn = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                Absent = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                Late = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                MissingOut = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceMissingOut),
                OnLeave = todayTimesheets.Count(t => t.Status == StatusEnum.AttendanceOnLeave),
                TotalExpected = todayTimesheets.Count
            };

            var deptMonthlyStats = new MonthlyAttendanceStatsDto
            {
                Month = month,
                Year = year,
                TotalWorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                TotalAbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes),
                TotalEmployeeRecords = monthTimesheets.Count
            };

            var draftPayrollCount = payrolls.Count(p => p.Status == PayrollStatus.Draft);
            var deptPendingAppealsCount = pendingAppeals.Count;

            var highLateCount = monthTimesheets
                .Where(t => t.Status == StatusEnum.AttendanceLate)
                .GroupBy(t => t.EmployeeId)
                .Count(g => g.Count() >= 3);

            var missingOutCount = monthTimesheets
                .Where(t => t.Status == StatusEnum.AttendanceMissingOut)
                .Select(t => t.EmployeeId)
                .Distinct()
                .Count();

            var alerts = BuildAlerts(deptPendingAppealsCount, draftPayrollCount, highLateCount, missingOutCount);

            return new ManagerDashboardDto
            {
                DeptEmployeeCount = deptEmployeeCount,
                EmployeesByDepartment = employeesByDepartment,
                DeptTodayAttendance = deptTodayAttendance,
                DeptMonthlyStats = deptMonthlyStats,
                DraftPayrollCount = draftPayrollCount,
                DeptPendingAppealsCount = deptPendingAppealsCount,
                Alerts = alerts
            };
        }

        public async Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(Guid userId, int month, int year)
        {
            var workDate = GetVietnamWorkDate();

            var employee = await _employeeRepo.GetByUserIdAsync(userId)
                ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ nhân sự cho tài khoản này.");

            var todayStatus = await _attendanceService.GetMyTodayStatusAsync(userId);

            var monthTimesheets = await _timesheetRepo.GetByEmployeeMonthAsync(employee.Id, month, year);

            var myMonthSummary = new MyMonthSummaryDto
            {
                Month = month,
                Year = year,
                WorkDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceNormal || t.Status == StatusEnum.AttendanceLate || t.Status == StatusEnum.AttendanceEarlyLeave || t.Status == StatusEnum.AttendancePresent),
                AbsentDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent),
                LateDays = monthTimesheets.Count(t => t.Status == StatusEnum.AttendanceLate),
                TotalOTHours = monthTimesheets.Sum(t => t.OTHours),
                TotalLateMinutes = monthTimesheets.Sum(t => t.TotalLateMinutes)
            };

            CurrentShiftDto? myCurrentShift = null;
            var (esp, definition) = await _shiftPatternRepo.GetActivePatternDetailsAsync(employee.Id, workDate);
            if (esp != null && definition != null && definition.CycleLengthDays > 0)
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
            var payrolls = await _payrollRepo.GetByEmployeeMonthAsync(employee.Id, employee.TenantId, month, year);
            var payroll = payrolls.FirstOrDefault();
            if (payroll != null && (payroll.Status == PayrollStatus.Published || payroll.Status == PayrollStatus.Paid))
            {
                myLatestPayroll = _mapper.Map<PayrollDto>(payroll);
            }

            var appeals = await _appealRepo.GetByEmployeeAsync(employee.Id);
            var myPendingAppealsCount = appeals.Count(a => a.Status == "PendingApproval");

            return new EmployeeDashboardDto
            {
                MyTodayStatus = todayStatus,
                MyMonthSummary = myMonthSummary,
                MyCurrentShift = myCurrentShift,
                MyLatestPayroll = myLatestPayroll,
                MyPendingAppealsCount = myPendingAppealsCount
            };
        }
    }
}
