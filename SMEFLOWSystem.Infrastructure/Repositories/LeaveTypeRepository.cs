using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class LeaveTypeRepository : ILeaveTypeRepository
{
    private readonly SMEFLOWSystemContext _context;

    public LeaveTypeRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<LeaveType?> GetByIdAsync(Guid id)
    {
        return await _context.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<LeaveType?> GetByCodeAsync(string code)
    {
        return await _context.LeaveTypes.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
    }

    public async Task<List<LeaveType>> GetAllAsync()
    {
        return await _context.LeaveTypes.Where(x => !x.IsDeleted).ToListAsync();
    }

    public async Task AddAsync(LeaveType leaveType)
    {
        await _context.LeaveTypes.AddAsync(leaveType);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(LeaveType leaveType)
    {
        _context.LeaveTypes.Update(leaveType);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(LeaveType leaveType)
    {
        leaveType.IsDeleted = true;
        _context.LeaveTypes.Update(leaveType);
        await _context.SaveChangesAsync();
    }
}
