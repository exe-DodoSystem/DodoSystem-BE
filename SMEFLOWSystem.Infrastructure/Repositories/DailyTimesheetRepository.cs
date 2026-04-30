using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class DailyTimesheetRepository : IDailyTimesheetRepository
{
    private readonly SMEFLOWSystemContext _context;

    public DailyTimesheetRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<DailyTimesheet?> GetByEmployeeDateAsync(Guid employeeId, DateOnly workDate)
    {
        return await _context.DailyTimesheets
            .Include(d => d.Segments)
            .Include(d => d.AuditLogs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.WorkDate == workDate);
    }

    public async Task<List<DailyTimesheet>> GetByEmployeeMonthAsync(Guid employeeId, int month, int year)
    {
        return await _context.DailyTimesheets
            .AsNoTracking()
            .Include(d => d.Segments)
            .AsSplitQuery()
            .Where(d => d.EmployeeId == employeeId && d.WorkDate.Month == month && d.WorkDate.Year == year)
            .OrderBy(d => d.WorkDate)
            .ToListAsync();
    }

    public async Task<List<DailyTimesheet>> GetByTenantMonthAsync(Guid tenantId, int month, int year)
    {
        return await _context.DailyTimesheets
            .AsNoTracking()
            .Include(d => d.Employee)
            .Include(d => d.Segments)
            .AsSplitQuery()
            .Where(d => d.TenantId == tenantId && d.WorkDate.Month == month && d.WorkDate.Year == year)
            .ToListAsync();
    }

    public async Task AddAsync(DailyTimesheet timesheet)
    {
        await _context.DailyTimesheets.AddAsync(timesheet);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(List<DailyTimesheet> timesheets)
    {
        await _context.DailyTimesheets.AddRangeAsync(timesheets);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DailyTimesheet timesheet)
    {
        _context.DailyTimesheets.Update(timesheet);
        await _context.SaveChangesAsync();
    }

    public async Task UpsertAsync(DailyTimesheet timesheet)
    {
        var existing = await _context.DailyTimesheets
            .FirstOrDefaultAsync(d => d.EmployeeId == timesheet.EmployeeId && d.WorkDate == timesheet.WorkDate);

        if (existing == null)
        {
            await _context.DailyTimesheets.AddAsync(timesheet);
        }
        else
        {
            timesheet.Id = existing.Id;
            _context.DailyTimesheets.Update(timesheet);
        }

        await _context.SaveChangesAsync();
    }
}
