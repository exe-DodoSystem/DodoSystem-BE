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

    public async Task<List<LeaveRequestSegment>> GetApprovedSegmentsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate)
    {
        return await _context.LeaveRequestSegments
            .AsNoTracking()
            .Include(s => s.LeaveRequest)
            .Where(s => s.LeaveDate >= minDate && s.LeaveDate <= maxDate
                        && s.LeaveRequest != null
                        && employeeIds.Contains(s.LeaveRequest.EmployeeId)
                        && s.LeaveRequest.Status == "Approved")
            .ToListAsync();
    }

    public async Task<LeaveRequest?> GetByIdAsync(Guid id)
    {
        return await _context.LeaveRequests
            .Include(r => r.Segments)
                .ThenInclude(s => s.TargetShiftSegment)
            .Include(r => r.Employee)
            .Include(r => r.LeaveTypeNavigation)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<LeaveRequest>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.LeaveRequests
            .Include(r => r.Segments)
                .ThenInclude(s => s.TargetShiftSegment)
            .Include(r => r.LeaveTypeNavigation)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.Segments.Min(s => (DateOnly?)s.LeaveDate))
            .ToListAsync();
    }

    public async Task<List<LeaveRequest>> GetPendingAsync()
    {
        return await _context.LeaveRequests
            .Include(r => r.Segments)
                .ThenInclude(s => s.TargetShiftSegment)
            .Include(r => r.Employee)
            .Include(r => r.LeaveTypeNavigation)
            .Where(r => r.Status == "Pending")
            .ToListAsync();
    }

    public async Task<List<LeaveRequest>> GetAllAsync()
    {
        return await _context.LeaveRequests
            .Include(r => r.Segments)
                .ThenInclude(s => s.TargetShiftSegment)
            .Include(r => r.Employee)
            .Include(r => r.LeaveTypeNavigation)
            .ToListAsync();
    }

    public async Task AddAsync(LeaveRequest leaveRequest)
    {
        await _context.LeaveRequests.AddAsync(leaveRequest);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(LeaveRequest leaveRequest)
    {
        _context.LeaveRequests.Update(leaveRequest);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(LeaveRequest leaveRequest)
    {
        _context.LeaveRequests.Remove(leaveRequest);
        await _context.SaveChangesAsync();
    }
}
