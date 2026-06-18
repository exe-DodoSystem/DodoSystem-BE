using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class BonusDeductionEntryRepository : IBonusDeductionEntryRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public BonusDeductionEntryRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task AddAsync(EmployeeBonusDeductionEntry entry)
        {
            await _context.EmployeeBonusDeductionEntries.AddAsync(entry);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<EmployeeBonusDeductionEntry> entries)
        {
            await _context.EmployeeBonusDeductionEntries.AddRangeAsync(entries);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(EmployeeBonusDeductionEntry entry)
        {
            _context.EmployeeBonusDeductionEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }

        public async Task<EmployeeBonusDeductionEntry?> GetByIdAsync(Guid id)
        {
            return await _context.EmployeeBonusDeductionEntries
                .Include(x => x.Employee)
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<EmployeeBonusDeductionEntry>> GetByTenantMonthYearAsync(Guid tenantId, int month, int year)
        {
            return await _context.EmployeeBonusDeductionEntries
                .AsNoTracking()
                .Include(x => x.Employee)
                .Include(x => x.CreatedByUser)
                .Where(x => x.TenantId == tenantId && x.Month == month && x.Year == year)
                .ToListAsync();
        }

        public async Task<List<EmployeeBonusDeductionEntry>> GetByEmployeeMonthYearAsync(Guid tenantId, Guid employeeId, int month, int year)
        {
            return await _context.EmployeeBonusDeductionEntries
                .AsNoTracking()
                .Include(x => x.Employee)
                .Include(x => x.CreatedByUser)
                .Where(x => x.TenantId == tenantId && x.EmployeeId == employeeId && x.Month == month && x.Year == year)
                .ToListAsync();
        }

        public async Task<(List<EmployeeBonusDeductionEntry> Items, int TotalCount)> GetPagedAsync(
            BonusDeductionEntryQueryDto query,
            Guid tenantId,
            List<Guid>? accessibleDepartmentIds)
        {
            var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

            var dbQuery = _context.EmployeeBonusDeductionEntries
                .AsNoTracking()
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Department)
                .Include(x => x.CreatedByUser)
                .Where(x => x.TenantId == tenantId)
                .AsQueryable();

            // Lọc theo phòng ban truy cập được (Manager)
            if (accessibleDepartmentIds != null)
            {
                dbQuery = dbQuery.Where(x => x.Employee.DepartmentId.HasValue && accessibleDepartmentIds.Contains(x.Employee.DepartmentId.Value));
            }

            // Lọc theo các bộ lọc khác
            if (query.EmployeeId.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.EmployeeId == query.EmployeeId.Value);
            }
            if (query.DepartmentId.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.Employee.DepartmentId == query.DepartmentId.Value);
            }
            if (query.Month.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.Month == query.Month.Value);
            }
            if (query.Year.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.Year == query.Year.Value);
            }
            if (query.Type.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.Type == query.Type.Value);
            }
            if (query.Category.HasValue)
            {
                dbQuery = dbQuery.Where(x => x.Category == query.Category.Value);
            }

            var totalCount = await dbQuery.CountAsync();
            var items = await dbQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
