using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShareKernel.Common.Enum;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class PayrollRepository : IPayrollRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public PayrollRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Payroll payroll)
        {
            await _context.Payrolls.AddAsync(payroll);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(List<Payroll> payrolls)
        {
            await _context.Payrolls.AddRangeAsync(payrolls);
            await _context.SaveChangesAsync();
        }

        public async Task<Payroll?> GetByIdAsync(Guid payrollId)
        {
            return await _context.Payrolls
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(p => p.Id == payrollId);
        }

        public async Task<List<Payroll>> GetByEmployeeMonthAsync(Guid employeeId, Guid tenantId, int month, int year)
        {
            return await _context.Payrolls
                .Where(p => p.EmployeeId == employeeId && p.TenantId == tenantId && p.Month == month && p.Year == year)
                .ToListAsync();
        }

        public async Task<List<Payroll>> GetByTenantMonthAsync(Guid tenantId, int month, int year)
        {
            return await _context.Payrolls
                .Where(p => p.TenantId == tenantId && p.Month == month && p.Year == year)
                .Where(p => p.Status == StatusEnum.PayrollDraft)
                .ToListAsync();
        }

        public async Task<List<Payroll>> GetDraftsByTenantMonthAsync(Guid tenantId, int month, int year)
        {
            return await _context.Payrolls
                .Where(p => p.TenantId == tenantId && p.Month == month && p.Year == year && p.Status == StatusEnum.PayrollDraft)
                .ToListAsync();
        }

        public async Task<Payroll> UpdateAsync(Payroll payroll)
        {
            _context.Payrolls.Update(payroll);
            await _context.SaveChangesAsync();
            return payroll;
        }

        public async Task<(List<Payroll> Items, int TotalCount)> GetPagedAsync(
            Guid tenantId,
            Guid? departmentId,
            Guid? employeeId,
            int? month,
            int? year,
            string? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            string? sortDir)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Payrolls
                .AsNoTracking()
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Department)
                .Where(p => p.TenantId == tenantId);

            if (departmentId.HasValue)
                query = query.Where(p => p.Employee.DepartmentId == departmentId.Value);

            if (employeeId.HasValue)
                query = query.Where(p => p.EmployeeId == employeeId.Value);

            if (month.HasValue)
                query = query.Where(p => p.Month == month.Value);

            if (year.HasValue)
                query = query.Where(p => p.Year == year.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(p => p.Status == status);

            // Sorting
            var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sortBy?.ToLower() switch
            {
                "employeename" => isDesc ? query.OrderByDescending(p => p.Employee.FullName) : query.OrderBy(p => p.Employee.FullName),
                "totalsalary" => isDesc ? query.OrderByDescending(p => p.TotalSalary) : query.OrderBy(p => p.TotalSalary),
                "status" => isDesc ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
                "month" => isDesc ? query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month) : query.OrderBy(p => p.Year).ThenBy(p => p.Month),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<Payroll> Items, int TotalCount)> GetByEmployeeIdPagedAsync(
            Guid employeeId,
            int? month,
            int? year,
            int pageNumber,
            int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Payrolls
                .AsNoTracking()
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Department)
                .Where(p => p.EmployeeId == employeeId);

            if (month.HasValue)
                query = query.Where(p => p.Month == month.Value);

            if (year.HasValue)
                query = query.Where(p => p.Year == year.Value);

            query = query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
