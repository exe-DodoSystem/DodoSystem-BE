using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.DTOs;
using ShareKernel.Common.Enum;
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
    public class PayrollService : IPayrollService
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDailyTimesheetRepository _timesheetRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<PayrollService> _logger;

        public PayrollService(
            IPayrollRepository payrollRepository,
            IEmployeeRepository employeeRepository,
            IDailyTimesheetRepository timesheetRepository,
            IMapper mapper,
            ILogger<PayrollService> logger)
        {
            _payrollRepository = payrollRepository;
            _employeeRepository = employeeRepository;
            _timesheetRepository = timesheetRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> GenerateMonthlyPayrollAsync(Guid tenantId, int month, int year)
        {
            // 1. Lấy tất cả nhân sự đang làm việc
            var employees = await _employeeRepository.GetAllActiveEmployeeByTenantId(tenantId);
            if (employees == null || !employees.Any()) return false;

            var existingPayrolls = await _payrollRepository.GetByTenantMonthAsync(tenantId, month, year);
            var newPayrolls = new List<Payroll>();
            var updatePayrolls = new List<Payroll>();

            foreach (var emp in employees)
            {
                var timesheets = await _timesheetRepository.GetByEmployeeMonthAsync(emp.Id, month, year);
                
                var existingPayroll = existingPayrolls.FirstOrDefault(p => p.EmployeeId == emp.Id);
                
                // Idempotent Check: Bỏ qua nếu đã Published
                if (existingPayroll != null && existingPayroll.Status == PayrollStatus.Published)
                    continue;

                // Tính Standard Working Days (Trừ T7, CN)
                int daysInMonth = DateTime.DaysInMonth(year, month);
                int standardDays = Enumerable.Range(1, daysInMonth)
                    .Select(day => new DateTime(year, month, day))
                    .Count(date => date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday);

                // Tính toán các chỉ số từ Timesheet
                int actualDays = timesheets.Count(t => t.ActualWorkHours > 0 || t.Status == "Present" || t.Status == "Late" || t.Status == "EarlyLeave");
                int lateMinutes = timesheets.Sum(t => t.TotalLateMinutes);
                int earlyLeaveMinutes = timesheets.Sum(t => t.TotalEarlyLeaveMinutes);
                int absentDays = timesheets.Count(t => t.Status == "Absent");
                decimal otHours = timesheets.Sum(t => t.OTHours);

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

            return true;
        }

        public async Task<PayrollDto> CalculatePayrollForEmployeeAsync(Guid tenantId, Guid employeeId, int month, int year)
        {
            var emp = await _employeeRepository.GetByIdAsync(employeeId);
            if (emp == null || emp.TenantId != tenantId) throw new Exception("Không tìm thấy nhân viên.");

            var existingPayrolls = await _payrollRepository.GetByEmployeeMonthAsync(employeeId, tenantId, month, year);
            var existingPayroll = existingPayrolls.FirstOrDefault();

            if (existingPayroll != null && existingPayroll.Status == PayrollStatus.Published)
                throw new Exception("Phiếu lương đã chốt (Published), không thể tính toán lại.");

            var timesheets = await _timesheetRepository.GetByEmployeeMonthAsync(employeeId, month, year);

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int standardDays = Enumerable.Range(1, daysInMonth)
                .Select(day => new DateTime(year, month, day))
                .Count(date => date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday);

            int actualDays = timesheets.Count(t => t.ActualWorkHours > 0 || t.Status == "Present" || t.Status == "Late" || t.Status == "EarlyLeave");
            int lateMinutes = timesheets.Sum(t => t.TotalLateMinutes);
            int earlyLeaveMinutes = timesheets.Sum(t => t.TotalEarlyLeaveMinutes);
            int absentDays = timesheets.Count(t => t.Status == "Absent");
            decimal otHours = timesheets.Sum(t => t.OTHours);

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
                return _mapper.Map<PayrollDto>(existingPayroll);
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
                return _mapper.Map<PayrollDto>(created ?? payroll);
            }
        }

        public async Task<PagedResultDto<PayrollDto>> GetPagedAsync(Guid tenantId, PayrollQueryDto query)
        {
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
                query.SortDir);

            var dtos = _mapper.Map<List<PayrollDto>>(items);

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

            // Lọc: Chỉ trả về phiếu lương đã Publish (hoặc Draft nếu theo yêu cầu đặc thù, nhưng chuẩn MVP là chỉ thấy Published)
            var publishedItems = items.Where(p => p.Status == PayrollStatus.Published).ToList();

            return _mapper.Map<List<PayrollDto>>(publishedItems);
        }

        public async Task<bool> ApproveAsync(Guid payrollId) => throw new NotImplementedException();
        public async Task<bool> RejectAsync(Guid payrollId, string reason) => throw new NotImplementedException();
        public async Task<bool> MarkPaidAsync(Guid payrollId) => throw new NotImplementedException();

        public async Task<PayrollDto> UpdateManualFieldsAsync(Guid payrollId, UpdatePayrollDto dto)
        {
            var payroll = await _payrollRepository.GetByIdAsync(payrollId);
            if (payroll == null) throw new Exception("Không tìm thấy phiếu lương.");

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

            if (payroll.Status == PayrollStatus.Published)
                throw new Exception("Phiếu lương này đã được chốt (Published) từ trước.");

            payroll.Status = PayrollStatus.Published;
            await _payrollRepository.UpdateAsync(payroll);
            return true;
        }
    }
}
