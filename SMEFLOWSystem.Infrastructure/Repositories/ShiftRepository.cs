using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ShiftRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<(List<Shift> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool includeDeleted,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Shifts
            .AsNoTracking()
            .Include(s => s.Segments)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Code.Contains(s) || x.Name.Contains(s));
        }

        var total = await query.CountAsync();
        query = query.OrderBy(x => x.Name).ThenBy(x => x.Code);

        var skip = (pageNumber - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task<Shift?> GetByIdWithSegmentsAsync(Guid id)
    {
        return _context.Shifts
            .Include(s => s.Segments)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddAsync(Shift shift)
    {
        await _context.Shifts.AddAsync(shift);
        await _context.SaveChangesAsync();
    }

    public async Task<Shift> UpdateAsync(Shift shift)
    {
        _context.Shifts.Update(shift);
        await _context.SaveChangesAsync();
        return shift;
    }

    public async Task SoftDeleteAsync(Shift shift)
    {
        shift.IsDeleted = true;
        _context.Shifts.Update(shift);
        await _context.SaveChangesAsync();
    }

    public Task<bool> HasUsageAsync(Guid shiftId)
    {
        return _context.ShiftPatternDays.AnyAsync(x => x.ScheduledShiftId == shiftId);
    }

    public async Task<bool> IsCodeOrNameExists(string code, string name)
    {
        return await _context.Shifts.AnyAsync(s => s.Code == code || s.Name == name);
    }
}
