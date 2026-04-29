using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly SMEFLOWSystemContext _context;

    public LeaveRequestRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<List<LeaveRequestSegment>> GetApprovedSegmentsByEmployeeDateAsync(Guid employeeId, DateOnly leaveDate)
    {
        return await _context.LeaveRequestSegments
            .AsNoTracking()
            .Include(s => s.LeaveRequest)
            .Where(s => s.LeaveDate == leaveDate
                        && s.LeaveRequest != null
                        && s.LeaveRequest.EmployeeId == employeeId
                        && s.LeaveRequest.Status == "Approved")
            .ToListAsync();
    }
}
