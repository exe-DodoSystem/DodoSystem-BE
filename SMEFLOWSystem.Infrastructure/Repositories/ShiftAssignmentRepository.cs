using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ShiftAssignmentRepository : IShiftAssignmentRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ShiftAssignmentRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<(List<EmployeeShiftPattern> Items, int TotalCount)> GetPagedAsync(
        Guid? employeeId,
        Guid? departmentId,
        Guid? shiftPatternId,
        bool? isActiveOnly,
        int pageNumber,
        int pageSize,
        DateOnly today)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.EmployeeShiftPatterns
            .AsNoTracking()
            .Include(x => x.Employee)
                .ThenInclude(e => e.Department)
            .Include(x => x.ShiftPattern)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        if (departmentId.HasValue)
            query = query.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
        if (shiftPatternId.HasValue)
            query = query.Where(x => x.ShiftPatternId == shiftPatternId.Value);
        if (isActiveOnly == true)
            query = query.Where(x => x.EffectiveEndDate == null || x.EffectiveEndDate >= today);

        var total = await query.CountAsync();
        query = query.OrderByDescending(x => x.EffectiveStartDate);

        var skip = (pageNumber - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task<EmployeeShiftPattern?> GetByIdAsync(Guid id)
    {
        return _context.EmployeeShiftPatterns
            .Include(x => x.Employee)
                .ThenInclude(e => e.Department)
            .Include(x => x.ShiftPattern)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task BulkEndPreviousAssignmentsAsync(IEnumerable<Guid> employeeIds, DateOnly newStartDate)
    {
        var endDate = newStartDate.AddDays(-1);
        await _context.EmployeeShiftPatterns
            .Where(x => employeeIds.Contains(x.EmployeeId)
                        && x.EffectiveStartDate <= newStartDate
                        && (x.EffectiveEndDate == null || x.EffectiveEndDate >= newStartDate))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.EffectiveEndDate, endDate));
    }

    public async Task BulkInsertAssignmentsAsync(List<EmployeeShiftPattern> assignments)
    {
        await _context.EmployeeShiftPatterns.AddRangeAsync(assignments);
        await _context.SaveChangesAsync();
    }
}
