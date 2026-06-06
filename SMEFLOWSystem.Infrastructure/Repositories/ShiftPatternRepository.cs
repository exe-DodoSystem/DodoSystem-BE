using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ShiftPatternRepository : IShiftPatternRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ShiftPatternRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<EmployeeShiftPattern?> GetActivePatternForEmployeeAsync(Guid employeeId, DateOnly targetDate)
    {
        return await _context.EmployeeShiftPatterns
            .AsNoTracking()
            .Where(esp => esp.EmployeeId == employeeId 
                          && esp.EffectiveStartDate <= targetDate 
                          && (esp.EffectiveEndDate == null || esp.EffectiveEndDate >= targetDate))
            .Join(
                _context.ShiftPatterns.Include(sp => sp.Days),
                esp => esp.ShiftPatternId,
                sp => sp.Id,
                (esp, sp) => new { EmployeeShiftPattern = esp, ShiftPattern = sp }
            )
            .Select(x => x.EmployeeShiftPattern)
            // LƯU Ý: Vì return ra EmployeeShiftPattern chưa có navigation prop tới ShiftPattern trong Entity gốc,
            // ở tầng Service chúng ta sẽ gọi _context.ShiftPatterns nếu cần.
            // Để đơn giản hơn tôi sẽ sửa lại method này trả về (EmployeeShiftPattern, ShiftPattern)
            .FirstOrDefaultAsync();
    }

    public async Task<List<EmployeeShiftPattern>> GetActivePatternsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate)
    {
        return await _context.EmployeeShiftPatterns
            .AsNoTracking()
            .Where(e => employeeIds.Contains(e.EmployeeId)
                        && e.EffectiveStartDate <= maxDate
                        && (e.EffectiveEndDate == null || e.EffectiveEndDate >= minDate))
            .ToListAsync();
    }

    public async Task<List<ShiftPattern>> GetPatternsWithDaysAsync(List<Guid> patternIds)
    {
        return await _context.ShiftPatterns
            .AsNoTracking()
            .Include(sp => sp.Days)
            .Where(sp => patternIds.Contains(sp.Id))
            .ToListAsync();
    }

    public async Task<List<Shift>> GetShiftsWithSegmentsAsync(List<Guid> shiftIds)
    {
        return await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Segments)
            .Where(s => shiftIds.Contains(s.Id))
            .ToListAsync();
    }

    // Viết lại hàm này để lấy rõ hơn
    public async Task<(EmployeeShiftPattern? Pattern, ShiftPattern? Definition)> GetActivePatternDetailsAsync(Guid employeeId, DateOnly targetDate)
    {
        var esp = await _context.EmployeeShiftPatterns
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                                      && e.EffectiveStartDate <= targetDate
                                      && (e.EffectiveEndDate == null || e.EffectiveEndDate >= targetDate));

        if (esp == null) return (null, null);

        var definition = await _context.ShiftPatterns
            .AsNoTracking()
            .Include(sp => sp.Days)
            .FirstOrDefaultAsync(sp => sp.Id == esp.ShiftPatternId);

        return (esp, definition);
    }

    public async Task<Shift?> GetShiftWithSegmentsAsync(Guid shiftId)
    {
         return await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Segments)
            .FirstOrDefaultAsync(s => s.Id == shiftId);
    }

    public async Task<ShiftPatternDay?> GetShiftPatternWithDaysAsync(Guid shiftPatternId, int dayIndex)
    {
        return await _context.ShiftPatternDays
            .AsNoTracking()
            .FirstOrDefaultAsync(spd => spd.ShiftPatternId == shiftPatternId
                                        && spd.DayIndex == dayIndex);
    }

    public async Task<(List<ShiftPattern> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool includeDeleted,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.ShiftPatterns
            .AsNoTracking()
            .Include(sp => sp.Days)
                .ThenInclude(d => d.ScheduledShift)
                    .ThenInclude(s => s.Segments)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Name.Contains(s));
        }

        var total = await query.CountAsync();
        query = query.OrderBy(x => x.Name);

        var skip = (pageNumber - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task<ShiftPattern?> GetByIdWithDaysAsync(Guid id)
    {
        return _context.ShiftPatterns
            .Include(sp => sp.Days)
                .ThenInclude(d => d.ScheduledShift)
                    .ThenInclude(s => s.Segments)
            .FirstOrDefaultAsync(sp => sp.Id == id);
    }

    public async Task AddAsync(ShiftPattern pattern)
    {
        await _context.ShiftPatterns.AddAsync(pattern);
        await _context.SaveChangesAsync();
    }

    public async Task<ShiftPattern> UpdateAsync(ShiftPattern pattern)
    {
        _context.ShiftPatterns.Update(pattern);
        await _context.SaveChangesAsync();
        return pattern;
    }

    public async Task DeleteAsync(ShiftPattern pattern)
    {
        _context.ShiftPatterns.Remove(pattern);
        await _context.SaveChangesAsync();
    }

    public Task<bool> HasUsageAsync(Guid shiftPatternId)
    {
        return _context.EmployeeShiftPatterns.AnyAsync(x => x.ShiftPatternId == shiftPatternId);
    }

    public Task<bool> ShiftExistsAsync(Guid shiftId)
    {
        return _context.Shifts.AnyAsync(x => x.Id == shiftId);
    }

    public async Task DeletePatternDaysAsync(Guid shiftPatternId)
    {
        var days = await _context.ShiftPatternDays.Where(x => x.ShiftPatternId == shiftPatternId).ToListAsync();
        _context.ShiftPatternDays.RemoveRange(days);
        await _context.SaveChangesAsync();
    }
}
