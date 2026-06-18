using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class EmployeeSalaryHistoryRepository : IEmployeeSalaryHistoryRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public EmployeeSalaryHistoryRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task AddAsync(EmployeeSalaryHistory history)
        {
            await _context.EmployeeSalaryHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<EmployeeSalaryHistory> Items, int TotalCount)> GetPagedByEmployeeIdAsync(
            Guid employeeId,
            int pageNumber,
            int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.EmployeeSalaryHistories
                .AsNoTracking()
                .Include(x => x.ChangedByUser)
                .Where(x => x.EmployeeId == employeeId);

            var total = await query.CountAsync();
            
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
    }
}
