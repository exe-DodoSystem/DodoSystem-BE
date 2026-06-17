using AutoMapper;
using Microsoft.Extensions.Logging;
using SharedKernel.DTOs;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using SMEFLOWSystem.Application.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class PayrollService : IPayrollService
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDailyTimesheetRepository _timesheetRepository;
        private readonly IPublicHolidayRepository _publicHolidayRepository;
        private readonly IManualMonthlyTimesheetRepository _manualTimesheetRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<PayrollService> _logger;
        private readonly IRealtimeNotificationService _realtime;
        private readonly IHrAuthorizationService _hrAuth;
        private readonly ICurrentUserService _currentUser;

        public PayrollService(
            IPayrollRepository payrollRepository,
            IEmployeeRepository employeeRepository,
            IDailyTimesheetRepository timesheetRepository,
            IPublicHolidayRepository publicHolidayRepository,
            IManualMonthlyTimesheetRepository manualTimesheetRepository,
            IMapper mapper,
            ILogger<PayrollService> logger,
            IRealtimeNotificationService realtime,
            IHrAuthorizationService hrAuth,
            ICurrentUserService currentUser)
        {
            _payrollRepository = payrollRepository;
            _employeeRepository = employeeRepository;
            _timesheetRepository = timesheetRepository;
            _publicHolidayRepository = publicHolidayRepository;
            _manualTimesheetRepository = manualTimesheetRepository;
            _mapper = mapper;
            _logger = logger;
            _realtime = realtime;
            _hrAuth = hrAuth;
            _currentUser = currentUser;
        }

        public async Task<bool> GenerateMonthlyPayrollAsync(Guid tenantId, int month, int year)
        {
            // 1. Lấy tất cả nhân sự đang làm việc
            var employees = await _employeeRepository.GetAllActiveEmployeeByTenantId(tenantId);
            if (employees == null || !employees.Any()) return false;

            var existingPayrolls = await _payrollRepository.GetByTenantMonthAsync(tenantId, month, year);
            var newPayrolls = new List<Payroll>();
            var updatePayrolls = new List<Payroll>();

            // Load ngày lễ của tenant một lần, dùng chung cho toàn bộ nhân viên
            var holidays = await _publicHolidayRepository.GetAllAsync(tenantId);
            var holidayDatesInMonth = holidays
                .Select(h => h.IsRecurringYearly
                    ? new DateOnly(year, h.Date.Month, h.Date.Day)
                    : h.Date)
                .Where(d => d.Month == month && d.Year == year)
                .ToHashSet();

            // Bulk load: 1 query duy nhất thay vì N queries
            var allTimesheets = await _timesheetRepository.GetByTenantMonthAsync(tenantId, month, year);
            var timesheetByEmployee = allTimesheets
                .GroupBy(t => t.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var allManualTimesheets = await _manualTimesheetRepository.GetByTenantMonthYearAsync(tenantId, month, year);
            var manualTimesheetByEmployee = allManualTimesheets.ToDictionary(t => t.EmployeeId);

            bool hasTimesheetData = allTimesheets.Any();

            // Tính 1 lần, dùng chung cho toàn bộ nhân viên
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int standardDays = Enumerable.Range(1, daysInMonth)
                .Select(day => new DateOnly(year, month, day))
                .Count(date => date.DayOfWeek != DayOfWeek.Saturday
                            && date.DayOfWeek != DayOfWeek.Sunday
                            && !holidayDatesInMonth.Contains(date));

            foreach (var emp in employees)
            {
                var timesheets = timesheetByEmployee.TryGetValue(emp.Id, out var ts)
                    ? ts
                    : new List<DailyTimesheet>();

                var existingPayroll = existingPayrolls.FirstOrDefault(p => p.EmployeeId == emp.Id);

                // Idempotent Check: Bỏ qua nếu đã Published hoặc Paid
                if (existingPayroll != null &&
                    (existingPayroll.Status == PayrollStatus.Published || existingPayroll.Status == PayrollStatus.Paid))
                    continue;

                var manualTimesheet = manualTimesheetByEmployee.TryGetValue(emp.Id, out var mt) ? mt : null;

                int actualDays;
                int lateMinutes;
                int earlyLeaveMinutes;
                int absentDays;
                decimal otHours;

                if (hasTimesheetData && timesheets.Any())
                {
                    // Tính toán các chỉ số từ Timesheet
                    actualDays = timesheets.Count(t =>
                        t.ActualWorkHours > 0 ||
                        t.Status == StatusEnum.AttendanceNormal     ||
                        t.Status == StatusEnum.AttendanceLate       ||
                        t.Status == StatusEnum.AttendanceEarlyLeave ||
                        t.Status == StatusEnum.AttendanceMissingOut ||
                        t.Status == StatusEnum.AttendanceOnLeave);
                    // Loại ngày MissingOut khỏi penalty — AttendanceResolution ghi earlyLeaveMinutes = gần cả ca
                    // cho ngày MissingOut, gây phạt quá nặng. Ngày MissingOut cần xử lý thủ công bởi HR.
                    lateMinutes = timesheets
                        .Where(t => t.Status != StatusEnum.AttendanceMissingOut)
                        .Sum(t => t.TotalLateMinutes);
                    earlyLeaveMinutes = timesheets
                        .Where(t => t.Status != StatusEnum.AttendanceMissingOut)
                        .Sum(t => t.TotalEarlyLeaveMinutes);
                    absentDays = timesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent);
                    otHours = timesheets.Sum(t => t.OTHours);
                }
                else if (manualTimesheet != null)
                {
                    // Lấy chỉ số từ bảng công nhập tay
                    actualDays = manualTimesheet.ActualWorkingDays;
                    lateMinutes = manualTimesheet.TotalLateMinutes;
                    earlyLeaveMinutes = manualTimesheet.TotalEarlyLeaveMinutes;
                    absentDays = manualTimesheet.AbsentDays;
                    otHours = manualTimesheet.TotalOTHours;
                }
                else
                {
                    // Fallback: không có dữ liệu chấm công
                    actualDays = standardDays;
                    lateMinutes = 0;
                    earlyLeaveMinutes = 0;
                    absentDays = 0;
                    otHours = 0;
                    _logger.LogWarning(
                        "Payroll fallback mode: Tenant {TenantId}, Employee {EmployeeId}, {Month}/{Year} — no timesheet data.",
                        tenantId, emp.Id, month, year);
                }

                // Nếu không đi làm ngày nào thì BasePay = 0
                decimal basePay = 0;
                decimal otPay = 0;
                decimal penaltyFee = 0;

                if (standardDays > 0)
                {
                    basePay = (emp.BaseSalary / standardDays) * actualDays;
                    
                    // Lương 1 giờ
                    decimal hourlyRate = (emp.BaseSalary / standardDays) / 8m;
                    
                    // OT Rate = 1.5
                    otPay = otHours * hourlyRate * 1.5m;
                    
                    // Phạt đi trễ / về sớm (Khấu trừ theo đúng số phút)
                    decimal minuteRate = hourlyRate / 60m;
                    penaltyFee = (lateMinutes + earlyLeaveMinutes) * minuteRate;
                }

                if (existingPayroll != null)
                {
                    // Cập nhật đè lên bản Draft cũ (Giữ nguyên CustomBonus/Deduction nếu có)
                    existingPayroll.StandardWorkingDays = standardDays;
                    existingPayroll.ActualWorkingDays = actualDays;
                    existingPayroll.TotalLateMinutes = lateMinutes;
                    existingPayroll.TotalEarlyLeaveMinutes = earlyLeaveMinutes;
                    existingPayroll.AbsentDays = absentDays;
                    existingPayroll.TotalOTHours = otHours;

                    existingPayroll.BaseSalarySnapshot = emp.BaseSalary;
                    existingPayroll.BasePay = Math.Round(basePay, 2);
                    existingPayroll.OTPay = Math.Round(otPay, 2);
                    existingPayroll.PenaltyFee = Math.Round(penaltyFee, 2);

                    existingPayroll.NetSalary = Math.Round(existingPayroll.BasePay + existingPayroll.OTPay - existingPayroll.PenaltyFee 
                                                + (existingPayroll.CustomBonus ?? 0) - existingPayroll.CustomDeduction, 2);
                    
                    updatePayrolls.Add(existingPayroll);
                }
                else
                {
                    // Sinh mới bản nháp Draft
                    var payroll = new Payroll
                    {
                        TenantId = tenantId,
                        EmployeeId = emp.Id,
                        Month = month,
                        Year = year,
                        Status = PayrollStatus.Draft,
                        
                        StandardWorkingDays = standardDays,
                        ActualWorkingDays = actualDays,
                        TotalLateMinutes = lateMinutes,
                        TotalEarlyLeaveMinutes = earlyLeaveMinutes,
                        AbsentDays = absentDays,
                        TotalOTHours = otHours,

                        BaseSalarySnapshot = emp.BaseSalary,
                        BasePay = Math.Round(basePay, 2),
                        OTPay = Math.Round(otPay, 2),
                        PenaltyFee = Math.Round(penaltyFee, 2),
                        CustomBonus = 0,
                        CustomDeduction = 0,
                    };
                    payroll.NetSalary = Math.Round(payroll.BasePay + payroll.OTPay - payroll.PenaltyFee, 2);
                    
                    newPayrolls.Add(payroll);
                }
            }

            if (newPayrolls.Any()) await _payrollRepository.AddRangeAsync(newPayrolls);
            if (updatePayrolls.Any()) await _payrollRepository.UpdateRangeAsync(updatePayrolls);

            return newPayrolls.Any() || updatePayrolls.Any();
        }

        public async Task<PayrollDto> CalculatePayrollForEmployeeAsync(Guid tenantId, Guid employeeId, int month, int year)
        {
            var emp = await _employeeRepository.GetByIdAsync(employeeId);
            if (emp == null || emp.TenantId != tenantId) throw new Exception("Không tìm thấy nhân viên.");

            if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            {
                await _hrAuth.EnsureEmployeeAccessAsync(emp);
            }

            var existingPayrolls = await _payrollRepository.GetByEmployeeMonthAsync(employeeId, tenantId, month, year);
            var existingPayroll = existingPayrolls.FirstOrDefault();

            if (existingPayroll != null && existingPayroll.Status != PayrollStatus.Draft)
                throw new Exception("Phiếu lương đã chốt, không thể tính toán lại.");

            var timesheets = await _timesheetRepository.GetByEmployeeMonthAsync(employeeId, month, year);

            var holidays = await _publicHolidayRepository.GetAllAsync(tenantId);
            var holidayDatesInMonth = holidays
                .Select(h => h.IsRecurringYearly
                    ? new DateOnly(year, h.Date.Month, h.Date.Day)
                    : h.Date)
                .Where(d => d.Month == month && d.Year == year)
                .ToHashSet();

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int standardDays = Enumerable.Range(1, daysInMonth)
                .Select(day => new DateOnly(year, month, day))
                .Count(date => date.DayOfWeek != DayOfWeek.Saturday
                            && date.DayOfWeek != DayOfWeek.Sunday
                            && !holidayDatesInMonth.Contains(date));

            var manualTimesheet = await _manualTimesheetRepository.GetByEmployeeMonthYearAsync(tenantId, employeeId, month, year);

            int actualDays;
            int lateMinutes;
            int earlyLeaveMinutes;
            int absentDays;
            decimal otHours;
            bool isTimesheetBased = timesheets.Any() || manualTimesheet != null;

            if (timesheets.Any())
            {
                actualDays = timesheets.Count(t =>
                    t.ActualWorkHours > 0 ||
                    t.Status == StatusEnum.AttendanceNormal     ||
                    t.Status == StatusEnum.AttendanceLate       ||
                    t.Status == StatusEnum.AttendanceEarlyLeave ||
                    t.Status == StatusEnum.AttendanceMissingOut ||
                    t.Status == StatusEnum.AttendanceOnLeave);
                // Loại ngày MissingOut khỏi penalty — AttendanceResolution ghi earlyLeaveMinutes = gần cả ca
                // cho ngày MissingOut, gây phạt quá nặng. Ngày MissingOut cần xử lý thủ công bởi HR.
                lateMinutes = timesheets
                    .Where(t => t.Status != StatusEnum.AttendanceMissingOut)
                    .Sum(t => t.TotalLateMinutes);
                earlyLeaveMinutes = timesheets
                    .Where(t => t.Status != StatusEnum.AttendanceMissingOut)
                    .Sum(t => t.TotalEarlyLeaveMinutes);
                absentDays = timesheets.Count(t => t.Status == StatusEnum.AttendanceAbsent);
                otHours = timesheets.Sum(t => t.OTHours);
            }
            else if (manualTimesheet != null)
            {
                actualDays = manualTimesheet.ActualWorkingDays;
                lateMinutes = manualTimesheet.TotalLateMinutes;
                earlyLeaveMinutes = manualTimesheet.TotalEarlyLeaveMinutes;
                absentDays = manualTimesheet.AbsentDays;
                otHours = manualTimesheet.TotalOTHours;
            }
            else
            {
                actualDays = standardDays;
                lateMinutes = 0;
                earlyLeaveMinutes = 0;
                absentDays = 0;
                otHours = 0;
                _logger.LogWarning(
                    "Payroll fallback mode: Tenant {TenantId}, Employee {EmployeeId}, {Month}/{Year} — no timesheet data.",
                    tenantId, employeeId, month, year);
            }

            decimal basePay = 0;
            decimal otPay = 0;
            decimal penaltyFee = 0;

            if (standardDays > 0)
            {
                basePay = (emp.BaseSalary / standardDays) * actualDays;
                decimal hourlyRate = (emp.BaseSalary / standardDays) / 8m;
                otPay = otHours * hourlyRate * 1.5m;
                decimal minuteRate = hourlyRate / 60m;
                penaltyFee = (lateMinutes + earlyLeaveMinutes) * minuteRate;
            }

            if (existingPayroll != null)
            {
                existingPayroll.StandardWorkingDays = standardDays;
                existingPayroll.ActualWorkingDays = actualDays;
                existingPayroll.TotalLateMinutes = lateMinutes;
                existingPayroll.TotalEarlyLeaveMinutes = earlyLeaveMinutes;
                existingPayroll.AbsentDays = absentDays;
                existingPayroll.TotalOTHours = otHours;

                existingPayroll.BaseSalarySnapshot = emp.BaseSalary;
                existingPayroll.BasePay = Math.Round(basePay, 2);
                existingPayroll.OTPay = Math.Round(otPay, 2);
                existingPayroll.PenaltyFee = Math.Round(penaltyFee, 2);

                existingPayroll.NetSalary = Math.Round(existingPayroll.BasePay + existingPayroll.OTPay - existingPayroll.PenaltyFee 
                                            + (existingPayroll.CustomBonus ?? 0) - existingPayroll.CustomDeduction, 2);
                
                await _payrollRepository.UpdateAsync(existingPayroll);
                var dto = _mapper.Map<PayrollDto>(existingPayroll);
                dto.IsTimesheetBased = isTimesheetBased;
                return dto;
            }
            else
            {
                var payroll = new Payroll
                {
                    TenantId = tenantId,
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    Status = PayrollStatus.Draft,
                    StandardWorkingDays = standardDays,
                    ActualWorkingDays = actualDays,
                    TotalLateMinutes = lateMinutes,
                    TotalEarlyLeaveMinutes = earlyLeaveMinutes,
                    AbsentDays = absentDays,
                    TotalOTHours = otHours,
                    BaseSalarySnapshot = emp.BaseSalary,
                    BasePay = Math.Round(basePay, 2),
                    OTPay = Math.Round(otPay, 2),
                    PenaltyFee = Math.Round(penaltyFee, 2),
                    CustomBonus = 0,
                    CustomDeduction = 0,
                };
                payroll.NetSalary = Math.Round(payroll.BasePay + payroll.OTPay - payroll.PenaltyFee, 2);
                
                await _payrollRepository.AddAsync(payroll);
                
                var created = await _payrollRepository.GetByIdAsync(payroll.Id);
                var dto = _mapper.Map<PayrollDto>(created ?? payroll);
                dto.IsTimesheetBased = isTimesheetBased;
                return dto;
            }
        }

        public async Task<PagedResultDto<PayrollDto>> GetPagedAsync(Guid tenantId, PayrollQueryDto query)
        {
            var accessibleDeptIds = await _hrAuth.GetAccessibleDepartmentIdsAsync();
            if (accessibleDeptIds != null)
            {
                if (query.DepartmentId.HasValue && !accessibleDeptIds.Contains(query.DepartmentId.Value))
                    throw new UnauthorizedAccessException("Forbidden");

                if (query.EmployeeId.HasValue)
                {
                    var emp = await _employeeRepository.GetByIdAsync(query.EmployeeId.Value);
                    if (emp == null) throw new KeyNotFoundException("Employee not found");
                    await _hrAuth.EnsureEmployeeAccessAsync(emp);
                }
            }

            var (items, totalCount) = await _payrollRepository.GetPagedAsync(
                tenantId,
                query.DepartmentId,
                query.EmployeeId,
                query.Month,
                query.Year,
                query.Status,
                query.PageNumber,
                query.PageSize,
                query.SortBy,
                query.SortDir,
                accessibleDeptIds);

            var dtos = _mapper.Map<List<PayrollDto>>(items);

            if (dtos.Any())
            {
                var month = query.Month ?? DateTime.UtcNow.Month;
                var year = query.Year ?? DateTime.UtcNow.Year;
                var allTimesheets = await _timesheetRepository.GetByTenantMonthAsync(tenantId, month, year);
                var timesheetEmployeeIds = allTimesheets.Select(t => t.EmployeeId).ToHashSet();

                var allManualTimesheets = await _manualTimesheetRepository.GetByTenantMonthYearAsync(tenantId, month, year);
                var manualEmployeeIds = allManualTimesheets.Select(t => t.EmployeeId).ToHashSet();

                foreach (var dto in dtos)
                {
                    dto.IsTimesheetBased = timesheetEmployeeIds.Contains(dto.EmployeeId) || manualEmployeeIds.Contains(dto.EmployeeId);
                }
            }

            return new PagedResultDto<PayrollDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<List<PayrollDto>> GetMyPayrollAsync(Guid tenantId, Guid userId, int? month, int? year)
        {
            // Lấy Employee của User hiện tại
            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null || employee.TenantId != tenantId)
                return new List<PayrollDto>();

            var (items, _) = await _payrollRepository.GetByEmployeeIdPagedAsync(
                employee.Id,
                month,
                year,
                pageNumber: 1,
                pageSize: 100); // Trả về tối đa 100 phiếu lương (khoảng 8 năm) cho App Mobile

            // Lọc: Chỉ trả về phiếu lương đã Publish hoặc Paid
            var visibleItems = items.Where(p => 
                p.Status == PayrollStatus.Published || 
                p.Status == PayrollStatus.Paid).ToList();

            var dtos = _mapper.Map<List<PayrollDto>>(visibleItems);

            if (dtos.Any())
            {
                foreach (var dto in dtos)
                {
                    var timesheets = await _timesheetRepository.GetByEmployeeMonthAsync(employee.Id, dto.Month, dto.Year);
                    var manualTimesheet = await _manualTimesheetRepository.GetByEmployeeMonthYearAsync(tenantId, employee.Id, dto.Month, dto.Year);
                    dto.IsTimesheetBased = timesheets.Any() || manualTimesheet != null;
                }
            }

            return dtos;
        }

        public async Task<bool> MarkPaidAsync(Guid payrollId)
        {
            var payroll = await _payrollRepository.GetByIdAsync(payrollId);
            if (payroll == null) return false;

            if (payroll.Status != PayrollStatus.Published)
                throw new Exception("Chỉ phiếu lương đã chốt (Published) mới được đánh dấu đã thanh toán.");

            payroll.Status = PayrollStatus.Paid;
            await _payrollRepository.UpdateAsync(payroll);
            return true;
        }

        public async Task<PayrollDto> UpdateManualFieldsAsync(Guid payrollId, UpdatePayrollDto dto)
        {
            var payroll = await _payrollRepository.GetByIdAsync(payrollId);
            if (payroll == null) throw new Exception("Không tìm thấy phiếu lương.");

            if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            {
                var employee = await _employeeRepository.GetByIdAsync(payroll.EmployeeId);
                if (employee == null) throw new KeyNotFoundException("Employee not found");
                await _hrAuth.EnsureEmployeeAccessAsync(employee);
            }

            if (payroll.Status != PayrollStatus.Draft)
                throw new Exception("Chỉ được cập nhật thông tin khi phiếu lương đang ở trạng thái Nháp (Draft).");

            payroll.CustomBonus = dto.CustomBonus;
            payroll.CustomDeduction = dto.CustomDeduction ?? 0;
            if (!string.IsNullOrEmpty(dto.Reason)) payroll.Notes = dto.Reason;

            payroll.NetSalary = Math.Round(payroll.BasePay + payroll.OTPay - payroll.PenaltyFee 
                                         + (payroll.CustomBonus ?? 0) - payroll.CustomDeduction, 2);

            await _payrollRepository.UpdateAsync(payroll);
            return _mapper.Map<PayrollDto>(payroll);
        }

        public async Task<bool> PublishPayrollAsync(Guid payrollId)
        {
            var payroll = await _payrollRepository.GetByIdAsync(payrollId);
            if (payroll == null) return false;

            if (payroll.Status != PayrollStatus.Draft)
                throw new Exception("Chỉ phiếu lương ở trạng thái Nháp (Draft) mới được chốt.");

            payroll.Status = PayrollStatus.Published;
            await _payrollRepository.UpdateAsync(payroll);

            // Emit realtime notification
            var employee = await _employeeRepository.GetByIdAsync(payroll.EmployeeId);
            if (employee?.UserId != null)
            {
                var payrollDto = new
                {
                    payrollId = payroll.Id,
                    month = payroll.Month,
                    year = payroll.Year,
                    netSalary = payroll.NetSalary
                };

                _ = _realtime.NotifyPayrollPublishedAsync(employee.UserId.Value, payrollDto)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogWarning(t.Exception, "Notify payroll.published failed for employee {UserId}", employee.UserId.Value);
                    });
            }

            return true;
        }

        public async Task<int> PublishAllDraftAsync(Guid tenantId, int month, int year)
        {
            var drafts = await _payrollRepository.GetDraftsByTenantMonthAsync(tenantId, month, year);
            if (!drafts.Any()) return 0;

            foreach (var payroll in drafts)
            {
                payroll.Status = PayrollStatus.Published;
            }
            
            await _payrollRepository.UpdateRangeAsync(drafts);

            // Emit realtime notification in bulk
            var employeeIds = drafts.Select(p => p.EmployeeId).Distinct().ToList();
            var employees = await _employeeRepository.GetByIdsAsync(employeeIds);
            var employeeMap = employees.ToDictionary(e => e.Id);

            foreach (var payroll in drafts)
            {
                if (employeeMap.TryGetValue(payroll.EmployeeId, out var employee) && employee?.UserId != null)
                {
                    var payrollDto = new
                    {
                        payrollId = payroll.Id,
                        month = payroll.Month,
                        year = payroll.Year,
                        netSalary = payroll.NetSalary
                    };

                    _ = _realtime.NotifyPayrollPublishedAsync(employee.UserId.Value, payrollDto)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                _logger.LogWarning(t.Exception, "Notify payroll.published failed for employee {UserId}", employee.UserId.Value);
                        });
                }
            }

            return drafts.Count;
        }

        public async Task<PayrollDto> SetBonusPenaltyByEmployeeAsync(Guid tenantId, EmployeeBonusPenaltyDto dto)
        {
            // Validate input
            if (dto.Month < 1 || dto.Month > 12)
                throw new ArgumentException("Tháng không hợp lệ (1-12).");
            if (dto.Year < 2000 || dto.Year > 2100)
                throw new ArgumentException("Năm không hợp lệ.");

            var emp = await _employeeRepository.GetByIdAsync(dto.EmployeeId);
            if (emp == null || emp.TenantId != tenantId)
                throw new KeyNotFoundException("Không tìm thấy nhân viên.");

            // Kiểm tra quyền truy cập
            if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
            {
                await _hrAuth.EnsureEmployeeAccessAsync(emp);
            }

            // Tìm payroll hiện có
            var existingPayrolls = await _payrollRepository.GetByEmployeeMonthAsync(dto.EmployeeId, tenantId, dto.Month, dto.Year);
            var payroll = existingPayrolls.FirstOrDefault();

            if (payroll != null && payroll.Status != PayrollStatus.Draft)
                throw new InvalidOperationException("Phiếu lương đã chốt, không thể thay đổi thưởng/phạt.");

            if (payroll == null)
            {
                // Tạo Payroll Draft mới nếu chưa tồn tại
                var holidays = await _publicHolidayRepository.GetAllAsync(tenantId);
                var holidayDatesInMonth = holidays
                    .Select(h => h.IsRecurringYearly
                        ? new DateOnly(dto.Year, h.Date.Month, h.Date.Day)
                        : h.Date)
                    .Where(d => d.Month == dto.Month && d.Year == dto.Year)
                    .ToHashSet();

                int daysInMonth = DateTime.DaysInMonth(dto.Year, dto.Month);
                int standardDays = Enumerable.Range(1, daysInMonth)
                    .Select(day => new DateOnly(dto.Year, dto.Month, day))
                    .Count(date => date.DayOfWeek != DayOfWeek.Saturday
                                && date.DayOfWeek != DayOfWeek.Sunday
                                && !holidayDatesInMonth.Contains(date));

                payroll = new Payroll
                {
                    TenantId = tenantId,
                    EmployeeId = dto.EmployeeId,
                    Month = dto.Month,
                    Year = dto.Year,
                    Status = PayrollStatus.Draft,
                    StandardWorkingDays = standardDays,
                    ActualWorkingDays = 0,
                    TotalLateMinutes = 0,
                    TotalEarlyLeaveMinutes = 0,
                    AbsentDays = 0,
                    TotalOTHours = 0,
                    BaseSalarySnapshot = emp.BaseSalary,
                    BasePay = 0,
                    OTPay = 0,
                    PenaltyFee = 0,
                    CustomBonus = 0,
                    CustomDeduction = 0,
                    NetSalary = 0
                };

                await _payrollRepository.AddAsync(payroll);
            }

            // Cập nhật thưởng/phạt
            if (dto.CustomBonus.HasValue)
                payroll.CustomBonus = dto.CustomBonus.Value;
            if (dto.CustomDeduction.HasValue)
                payroll.CustomDeduction = dto.CustomDeduction.Value;
            if (!string.IsNullOrEmpty(dto.Reason))
                payroll.Notes = dto.Reason;

            // Tính lại NetSalary
            payroll.NetSalary = Math.Round(
                payroll.BasePay + payroll.OTPay - payroll.PenaltyFee
                + (payroll.CustomBonus ?? 0) - payroll.CustomDeduction, 2);

            await _payrollRepository.UpdateAsync(payroll);

            // Reload với Include Employee+Department
            var reloaded = await _payrollRepository.GetByIdAsync(payroll.Id);
            return _mapper.Map<PayrollDto>(reloaded ?? payroll);
        }

        public async Task<List<PayrollDto>> BulkSetBonusPenaltyAsync(Guid tenantId, BulkBonusPenaltyDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new ArgumentException("Danh sách thưởng/phạt không được rỗng.");

            var results = new List<PayrollDto>();
            foreach (var item in dto.Items)
            {
                var result = await SetBonusPenaltyByEmployeeAsync(tenantId, item);
                results.Add(result);
            }

            return results;
        }
    }
}
