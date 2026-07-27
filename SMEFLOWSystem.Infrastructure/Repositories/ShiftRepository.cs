using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly SMEFLOWSystemContext _context;
    private readonly ICurrentTenantService _currentTenantService;

    public ShiftRepository(
        SMEFLOWSystemContext context,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _currentTenantService = currentTenantService;
    }

    public async Task<(List<Shift> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool includeDeleted,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var tenantId = _currentTenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant ID is missing.");

        IQueryable<Shift> query = includeDeleted
            ? _context.Shifts
                .IgnoreQueryFilters()
                .Where(shift => shift.TenantId == tenantId)
            : _context.Shifts;

        query = query
            .AsNoTracking()
            .Include(s => s.Segments.Where(
                segment => segment.TenantId == tenantId))
            .AsQueryable();

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

    public async Task DeleteAsync(Shift shift)
    {
        _context.Shifts.Remove(shift);
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
