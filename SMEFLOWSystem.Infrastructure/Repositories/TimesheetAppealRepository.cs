using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class TimesheetAppealRepository : ITimesheetAppealRepository
{
    private readonly SMEFLOWSystemContext _context;

    public TimesheetAppealRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TimesheetAppeal appeal)
    {
        await _context.Set<TimesheetAppeal>().AddAsync(appeal);
        await _context.SaveChangesAsync();
    }

    public async Task<TimesheetAppeal?> GetByIdAsync(Guid id)
    {
        return await _context.Set<TimesheetAppeal>()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<TimesheetAppeal>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<TimesheetAppeal>()
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.WorkDate)
            .ToListAsync();
    }

    public async Task<List<TimesheetAppeal>> GetPendingAsync(Guid tenantId)
    {
        return await _context.Set<TimesheetAppeal>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == "PendingApproval")
            .OrderBy(x => x.WorkDate)
            .ToListAsync();
    }

    public async Task UpdateAsync(TimesheetAppeal appeal)
    {
        _context.Set<TimesheetAppeal>().Update(appeal);
        await _context.SaveChangesAsync();
    }
}
