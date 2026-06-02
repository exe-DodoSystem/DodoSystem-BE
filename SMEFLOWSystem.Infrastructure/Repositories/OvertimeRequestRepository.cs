using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class OvertimeRequestRepository : IOvertimeRequestRepository
{
    private readonly SMEFLOWSystemContext _context;

    public OvertimeRequestRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<OvertimeRequest?> GetApprovedByEmployeeDateAsync(Guid employeeId, DateOnly overtimeDate)
    {
        return await _context.OvertimeRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId
                                      && x.OvertimeDate == overtimeDate
                                      && x.Status == "Approved");
    }

    public async Task<List<OvertimeRequest>> GetApprovedRequestsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate)
    {
        return await _context.OvertimeRequests
            .AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId)
                        && x.OvertimeDate >= minDate && x.OvertimeDate <= maxDate
                        && x.Status == "Approved")
            .ToListAsync();
    }
}
