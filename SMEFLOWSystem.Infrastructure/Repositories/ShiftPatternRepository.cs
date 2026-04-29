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
}
