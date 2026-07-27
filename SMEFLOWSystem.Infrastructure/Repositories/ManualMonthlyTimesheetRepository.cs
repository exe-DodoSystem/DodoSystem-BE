using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ManualMonthlyTimesheetRepository : IManualMonthlyTimesheetRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ManualMonthlyTimesheetRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ManualMonthlyTimesheet timesheet)
    {
        await _context.ManualMonthlyTimesheets.AddAsync(timesheet);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ManualMonthlyTimesheet timesheet)
    {
        _context.ManualMonthlyTimesheets.Update(timesheet);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ManualMonthlyTimesheet timesheet)
    {
        _context.ManualMonthlyTimesheets.Remove(timesheet);
        await _context.SaveChangesAsync();
    }

    public async Task<ManualMonthlyTimesheet?> GetByIdAsync(Guid id)
    {
        return await _context.ManualMonthlyTimesheets
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<ManualMonthlyTimesheet?> GetByEmployeeMonthYearAsync(Guid tenantId, Guid employeeId, int month, int year)
    {
        return await _context.ManualMonthlyTimesheets
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.EmployeeId == employeeId && x.Month == month && x.Year == year);
    }

    public async Task<List<ManualMonthlyTimesheet>> GetByTenantMonthYearAsync(
        Guid tenantId,
        int month,
        int year,
        IReadOnlyCollection<Guid>? departmentIds)
    {
        if (departmentIds is { Count: 0 })
        {
            return new List<ManualMonthlyTimesheet>();
        }

        var query = _context.ManualMonthlyTimesheets
            .Include(x => x.Employee)
            .Where(x =>
                x.TenantId == tenantId &&
                x.Month == month &&
                x.Year == year);

        if (departmentIds != null)
        {
            query = query.Where(x =>
                x.Employee.DepartmentId.HasValue &&
                departmentIds.Contains(x.Employee.DepartmentId.Value));
        }

        return await query.ToListAsync();
    }
}
