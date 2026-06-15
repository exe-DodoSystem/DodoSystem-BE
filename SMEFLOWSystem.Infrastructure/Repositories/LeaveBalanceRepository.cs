using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class LeaveBalanceRepository : ILeaveBalanceRepository
{
    private readonly SMEFLOWSystemContext _context;

    public LeaveBalanceRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<EmployeeLeaveBalance?> GetByIdAsync(Guid id)
    {
        return await _context.EmployeeLeaveBalances.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<EmployeeLeaveBalance?> GetByEmployeeTypeYearAsync(Guid employeeId, Guid leaveTypeId, int year)
    {
        return await _context.EmployeeLeaveBalances
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.Year == year);
    }

    public async Task<List<EmployeeLeaveBalance>> GetByEmployeeAsync(Guid employeeId, int year)
    {
        return await _context.EmployeeLeaveBalances
            .Where(x => x.EmployeeId == employeeId && x.Year == year)
            .ToListAsync();
    }

    public async Task<List<EmployeeLeaveBalance>> GetAllAsync(int year)
    {
        return await _context.EmployeeLeaveBalances
            .Where(x => x.Year == year)
            .ToListAsync();
    }

    public async Task AddAsync(EmployeeLeaveBalance balance)
    {
        await _context.EmployeeLeaveBalances.AddAsync(balance);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EmployeeLeaveBalance balance)
    {
        _context.EmployeeLeaveBalances.Update(balance);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<EmployeeLeaveBalance> balances)
    {
        await _context.EmployeeLeaveBalances.AddRangeAsync(balances);
        await _context.SaveChangesAsync();
    }
}
