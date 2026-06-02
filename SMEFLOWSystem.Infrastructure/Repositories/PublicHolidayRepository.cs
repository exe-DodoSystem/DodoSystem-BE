using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class PublicHolidayRepository : IPublicHolidayRepository
{
    private readonly SMEFLOWSystemContext _context;

    public PublicHolidayRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PublicHoliday holiday)
    {
        await _context.PublicHolidays.AddAsync(holiday);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PublicHoliday>> GetAllAsync(Guid tenantId)
    {
        // Global query filter handles tenant filtering, but explicit filter is safe
        return await _context.PublicHolidays
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Date)
            .ToListAsync();
    }

    public async Task<PublicHoliday?> GetByIdAsync(Guid id)
    {
        return await _context.PublicHolidays
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task DeleteAsync(PublicHoliday holiday)
    {
        _context.PublicHolidays.Remove(holiday);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsPublicHolidayAsync(Guid tenantId, DateOnly date)
    {
        return await _context.PublicHolidays
            .AnyAsync(h => h.TenantId == tenantId &&
                (h.Date == date ||
                (h.IsRecurringYearly && h.Date.Month == date.Month && h.Date.Day == date.Day)));
    }
}
