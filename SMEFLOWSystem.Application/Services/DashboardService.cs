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

            var frequentAbsentCount = monthTimesheets
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
            var missingOutCount = missingOutEmpIds.Except(appealedEmpIds).Count();

            var alerts = BuildAlerts(pendingAppealsCount, payrollSummary.DraftCount, frequentAbsentCount, missingOutCount);

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

            var frequentAbsentCount = monthTimesheets
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
            var missingOutCount = missingOutEmpIds.Except(appealedEmpIds).Count();

            var alerts = BuildAlerts(deptPendingAppealsCount, draftPayrollCount, frequentAbsentCount, missingOutCount);

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

            var todayStatusTask = _attendanceService.GetMyTodayStatusAsync(userId);
            var monthTimesheetsTask = _timesheetRepo.GetByEmployeeMonthAsync(employee.Id, month, year);
            var shiftTask = _shiftPatternRepo.GetActivePatternDetailsAsync(employee.Id, workDate);
            var payrollsTask = _payrollRepo.GetByEmployeeMonthAsync(employee.Id, employee.TenantId, month, year);
            var appealsTask = _appealRepo.GetByEmployeeAsync(employee.Id);

            await Task.WhenAll(todayStatusTask, monthTimesheetsTask, shiftTask, payrollsTask, appealsTask);

            var todayStatus = await todayStatusTask;
            var monthTimesheets = await monthTimesheetsTask;
            var (esp, definition) = await shiftTask;
            var payrolls = await payrollsTask;
            var appeals = await appealsTask;

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
            var payroll = payrolls.FirstOrDefault();
            if (payroll != null && (payroll.Status == PayrollStatus.Published || payroll.Status == PayrollStatus.Paid))
            {
                myLatestPayroll = _mapper.Map<PayrollDto>(payroll);
            }

            var myPendingAppealsCount = appeals.Count(a => a.Status == StatusEnum.ApprovalPending);

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
