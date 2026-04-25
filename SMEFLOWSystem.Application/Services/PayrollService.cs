using AutoMapper;
using SharedKernel.DTOs;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class PayrollService : IPayrollService
    {
        private readonly IPayrollRepository _payrollRepo;
        private readonly IAttendanceRepository _attendanceRepo;
        private readonly IEmployeeRepository _employeeRepo;
        private readonly ICurrentUserService _currentUser;
        private readonly ICurrentTenantService _currentTenant;
        private readonly INotificationService _notificationService;
        private readonly ITenantRepository _tenantRepo;
        private readonly IMapper _mapper;
        private readonly TimeProvider _timeProvider;

        public PayrollService(
            IPayrollRepository payrollRepo,
            IAttendanceRepository attendanceRepo,
            IEmployeeRepository employeeRepo,
            ICurrentUserService currentUser,
            ICurrentTenantService currentTenant,
            INotificationService notificationService,
            ITenantRepository tenantRepo,
            IMapper mapper,
            TimeProvider timeProvider)
        {
            _payrollRepo = payrollRepo;
            _attendanceRepo = attendanceRepo;
            _employeeRepo = employeeRepo;
            _currentUser = currentUser;
            _currentTenant = currentTenant;
            _notificationService = notificationService;
            _tenantRepo = tenantRepo;
            _mapper = mapper;
            _timeProvider = timeProvider;
        }
        // 1. Generate Draft cho toàn bộ NV
        public async Task<bool> GenerateMonthlyPayrollAsync(Guid tenantId, int month, int year)
        {
            _currentUser.EnsureAdmin();
            var currentTenantId = RequireTenantId();
            if (tenantId != currentTenantId)
                throw new UnauthorizedAccessException("Bạn không có quyền tạo lương cho công ty khác.");

            if (month < 1 || month > 12)
                throw new ArgumentException("Tháng phải nằm trong khoảng 1 - 12.");

            var now = _timeProvider.GetUtcNow();
            if (year > now.Year || (year == now.Year && month > now.Month))
                throw new ArgumentException("Không thể tạo lương cho tháng tương lai.");

            var activeEmployees = await _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId);
            if (activeEmployees.Count == 0)
                throw new InvalidOperationException("Không có nhân viên nào đang hoạt động trong công ty.");

            var existingPayrolls = await _payrollRepo.GetByTenantMonthAsync(tenantId, month, year);
            var existingEmployeeIds = existingPayrolls.Select(p => p.EmployeeId).ToHashSet();

            var standardWorkDays = GetStandardWorkingDays(year, month);
            var newPayrolls = new List<Payroll>();

            foreach (var employee in activeEmployees)
            {
                if (existingEmployeeIds.Contains(employee.Id))
                    continue;

                var attendances = await _attendanceRepo.GetByEmployeeMonthAsync(employee.Id, month, year);

                var actualWorkDays = attendances.Count(a => a.CheckInTime != null && a.Status != StatusEnum.AttendanceAbsent);
                var totalLateMinutes = attendances.Sum(a => a.LateMinutes ?? 0);
                var totalEarlyLeaveMinutes = attendances.Sum(a => a.EarlyLeaveMinutes ?? 0);
                var absentDays = attendances.Count(a => a.Status == StatusEnum.AttendanceAbsent);

                var baseSalary = employee.BaseSalary;
                var basePay = standardWorkDays == 0
                    ? 0
                    : Math.Round(baseSalary / standardWorkDays * actualWorkDays, 0, MidpointRounding.AwayFromZero);

                var deduction = CalculateDeduction(totalLateMinutes, totalEarlyLeaveMinutes, absentDays);

                var totalSalary = basePay - deduction;
                if (totalSalary < 0) totalSalary = 0;

                newPayrolls.Add(new Payroll
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EmployeeId = employee.Id,
                    Month = month,
                    Year = year,
                    StandardWorkingDays = standardWorkDays,
                    ActualWorkingDays = actualWorkDays,
                    TotalLateMinutes = totalLateMinutes,
                    TotalEarlyLeaveMinutes = totalEarlyLeaveMinutes,
                    AbsentDays = absentDays,
                    BaseSalarySnapshot = baseSalary,
                    BasePay = basePay,
                    Bonus = null,       
                    Deduction = deduction,
                    TotalSalary = totalSalary,
                    Status = StatusEnum.PayrollDraft,
                    Notes = null,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (newPayrolls.Count == 0)
                return false; 

            await _payrollRepo.AddRangeAsync(newPayrolls);
            return true;
        }

        // 2. Tính lương 1 NV
        public async Task<PayrollDto> CalculatePayrollForEmployeeAsync(Guid tenantId, Guid employeeId, int month, int year)
        {
            _currentUser.EnsureHrAccess();
            var currentTenantId = RequireTenantId();
            if (tenantId != currentTenantId)
                throw new UnauthorizedAccessException("Bạn không có quyền tính lương cho công ty khác.");

            var employee = await _employeeRepo.GetByIdAsync(employeeId)
                ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");

            if (employee.TenantId != tenantId)
                throw new UnauthorizedAccessException("Nhân viên không thuộc công ty này.");

            var existing = await _payrollRepo.GetByEmployeeMonthAsync(employeeId, tenantId, month, year);
            var payroll = existing.FirstOrDefault();

            if (payroll != null && payroll.Status != StatusEnum.PayrollDraft && payroll.Status != StatusEnum.PayrollRejected)
                throw new InvalidOperationException($"Phiếu lương đang ở trạng thái '{payroll.Status}', không thể tính lại.");

            var attendances = await _attendanceRepo.GetByEmployeeMonthAsync(employeeId, month, year);
            var standardWorkDays = GetStandardWorkingDays(year, month);

            var actualWorkDays = attendances.Count(a => a.CheckInTime != null && a.Status != StatusEnum.AttendanceAbsent);
            var totalLateMinutes = attendances.Sum(a => a.LateMinutes ?? 0);
            var totalEarlyLeaveMinutes = attendances.Sum(a => a.EarlyLeaveMinutes ?? 0);
            var absentDays = attendances.Count(a => a.Status == StatusEnum.AttendanceAbsent);

            var baseSalary = employee.BaseSalary;
            var basePay = standardWorkDays == 0
                ? 0
                : Math.Round(baseSalary / standardWorkDays * actualWorkDays, 0, MidpointRounding.AwayFromZero);

            var deduction = CalculateDeduction(totalLateMinutes, totalEarlyLeaveMinutes, absentDays);

            bool isNew = payroll == null;
            if (isNew)
            {
                payroll = new Payroll
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    CreatedAt = DateTime.UtcNow
                };
            }

            payroll.StandardWorkingDays = standardWorkDays;
            payroll.ActualWorkingDays = actualWorkDays;
            payroll.TotalLateMinutes = totalLateMinutes;
            payroll.TotalEarlyLeaveMinutes = totalEarlyLeaveMinutes;
            payroll.AbsentDays = absentDays;
            payroll.BaseSalarySnapshot = baseSalary;
            payroll.BasePay = basePay;
            payroll.Deduction = deduction;
            payroll.TotalSalary = RecalculateTotal(basePay, payroll.Bonus, deduction);
            payroll.Status = StatusEnum.PayrollDraft;
            payroll.UpdatedAt = DateTime.UtcNow;

            if (isNew)
                await _payrollRepo.AddAsync(payroll);
            else
                await _payrollRepo.UpdateAsync(payroll);

            var result = await _payrollRepo.GetByIdAsync(payroll.Id);
            return _mapper.Map<PayrollDto>(result);
        }

        // 3. GetPaged (Admin/Manager)
        public async Task<PagedResultDto<PayrollDto>> GetPagedAsync(PayrollQueryDto query)
        {
            _currentUser.EnsureHrAccess();
            var tenantId = RequireTenantId();

            var (items, total) = await _payrollRepo.GetPagedAsync(
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

            return new PagedResultDto<PayrollDto>
            {
                Items = _mapper.Map<List<PayrollDto>>(items),
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        // 4. NV xem lương mình
        public async Task<List<PayrollDto>> GetMyPayrollAsync(int? month, int? year)
        {
            var userId = _currentUser.RequireUserId();

            var employee = await _employeeRepo.GetByUserIdAsync(userId)
                ?? throw new UnauthorizedAccessException("Tài khoản chưa liên kết nhân viên.");

            var (items, _) = await _payrollRepo.GetByEmployeeIdPagedAsync(
                employee.Id, month, year, pageNumber: 1, pageSize: 100);

            return _mapper.Map<List<PayrollDto>>(items);
        }

        // 5. TenantAdmin duyệt
        public async Task<bool> ApproveAsync(Guid payrollId)
        {
            _currentUser.EnsureAdmin();

            var payroll = await _payrollRepo.GetByIdAsync(payrollId)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu lương.");

            ValidateTenantOwnership(payroll);

            if (payroll.Status != StatusEnum.PayrollDraft)
                throw new InvalidOperationException("Chỉ có thể duyệt phiếu lương ở trạng thái Draft.");

            payroll.Status = StatusEnum.PayrollApproved;
            payroll.UpdatedAt = DateTime.UtcNow;

            await _payrollRepo.UpdateAsync(payroll);
            return true;
        }

        // 6. TenantAdmin từ chối
        public async Task<bool> RejectAsync(Guid payrollId, string reason)
        {
            _currentUser.EnsureAdmin();

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Vui lòng nhập lý do từ chối.");

            var payroll = await _payrollRepo.GetByIdAsync(payrollId)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu lương.");

            ValidateTenantOwnership(payroll);

            if (payroll.Status != StatusEnum.PayrollDraft)
                throw new InvalidOperationException("Chỉ có thể từ chối phiếu lương ở trạng thái Draft.");

            payroll.Status = StatusEnum.PayrollRejected;
            payroll.Notes = reason;
            payroll.UpdatedAt = DateTime.UtcNow;

            await _payrollRepo.UpdateAsync(payroll);
            return true;
        }

        // 7. TenantAdmin xác nhận đã trả lương
        public async Task<bool> MarkPaidAsync(Guid payrollId)
        {
            _currentUser.EnsureAdmin();

            var payroll = await _payrollRepo.GetByIdAsync(payrollId)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu lương.");

            ValidateTenantOwnership(payroll);

            if (payroll.Status != StatusEnum.PayrollApproved)
                throw new InvalidOperationException("Chỉ có thể đánh dấu đã trả cho phiếu lương đã được duyệt (Approved).");

            payroll.Status = StatusEnum.PayrollPaid;
            payroll.UpdatedAt = DateTime.UtcNow;

            await _payrollRepo.UpdateAsync(payroll);
            return true;
        }

        // 8. Admin/HR chỉnh Bonus/Deduction
        public async Task<PayrollDto> UpdateBonusDeductionAsync(Guid payrollId, UpdatePayrollDto dto)
        {
            _currentUser.EnsureHrAccess();

            var payroll = await _payrollRepo.GetByIdAsync(payrollId)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu lương.");

            ValidateTenantOwnership(payroll);

            // Chỉ cho chỉnh sửa khi Draft hoặc Rejected
            if (payroll.Status != StatusEnum.PayrollDraft && payroll.Status != StatusEnum.PayrollRejected)
                throw new InvalidOperationException($"Không thể chỉnh sửa khi phiếu lương ở trạng thái '{payroll.Status}'.");

            // Gán giá trị mới
            if (dto.Bonus.HasValue)
                payroll.Bonus = dto.Bonus.Value;

            if (dto.Deduction.HasValue)
                payroll.Deduction = dto.Deduction.Value;

            if (!string.IsNullOrWhiteSpace(dto.Reason))
                payroll.Notes = dto.Reason;

            // Tính lại TotalSalary
            payroll.TotalSalary = RecalculateTotal(payroll.BasePay, payroll.Bonus, payroll.Deduction);
            payroll.Status = StatusEnum.PayrollDraft; // Nếu bị Rejected, update xong quay lại Draft
            payroll.UpdatedAt = DateTime.UtcNow;

            await _payrollRepo.UpdateAsync(payroll);

            await NotifyAdminPayrollUpdatedAsync(payroll);

            return _mapper.Map<PayrollDto>(payroll);
        }

        /// Gửi notification in-app cho TenantAdmin khi HR chỉnh sửa Bonus/Deduction
        private async Task NotifyAdminPayrollUpdatedAsync(Payroll payroll)
        {
            var tenant = await _tenantRepo.GetByIdAsync(payroll.TenantId);
            if (tenant?.OwnerUserId == null) return;

            var employeeName = payroll.Employee?.FullName ?? "N/A";

            await _notificationService.CreateAsync(
                tenantId: payroll.TenantId,
                recipientUserId: tenant.OwnerUserId.Value,
                title: $"Phiếu lương T{payroll.Month}/{payroll.Year} của {employeeName} đã được chỉnh sửa",
                message: $"Bonus: {payroll.Bonus:N0}đ | Deduction: {payroll.Deduction:N0}đ | Tổng: {payroll.TotalSalary:N0}đ. Vui lòng vào duyệt.",
                type: "PayrollUpdated",
                referenceId: payroll.Id);
        }

        // Private Helpers
        // Tính số ngày làm việc chuẩn (loại trừ T7, CN)
        private static int GetStandardWorkingDays(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            int workingDays = 0;
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    workingDays++;
            }
            return workingDays;
        }

        // Tính tiền phạt tự động dựa trên phút trễ + phút về sớm + ngày vắng
        private static decimal CalculateDeduction(int lateMinutes, int earlyLeaveMinutes, int absentDays)
        {
            const decimal penaltyPerLateMinute = 5_000m;        // 5.000đ / phút trễ
            const decimal penaltyPerEarlyMinute = 5_000m;       // 5.000đ / phút về sớm
            const decimal penaltyPerAbsentDay = 200_000m;       // 200.000đ / ngày nghỉ không phép

            return (lateMinutes * penaltyPerLateMinute)
                 + (earlyLeaveMinutes * penaltyPerEarlyMinute)
                 + (absentDays * penaltyPerAbsentDay);
        }

        // Tính lại TotalSalary = BasePay + Bonus - Deduction
        private static decimal RecalculateTotal(decimal basePay, decimal? bonus, decimal deduction)
        {
            var total = basePay + (bonus ?? 0) - deduction;
            return total < 0 ? 0 : Math.Round(total, 0, MidpointRounding.AwayFromZero);
        }

        // Lấy TenantId từ JWT, throw nếu không có
        private Guid RequireTenantId()
        {
            return _currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("Không xác định được công ty.");
        }

        // Kiểm tra Payroll có thuộc Tenant hiện tại không
        private void ValidateTenantOwnership(Payroll payroll)
        {
            var tenantId = RequireTenantId();
            if (payroll.TenantId != tenantId)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên phiếu lương này.");
        }
    }
}
