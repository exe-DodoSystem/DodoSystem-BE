using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ILeaveRequestRepository
{
    /// <summary>
    /// Lấy tất cả LeaveRequestSegment đã được duyệt (Approved) cho một nhân viên trong một ngày cụ thể.
    /// Join qua LeaveRequest để filter theo Status = "Approved".
    /// </summary>
    Task<List<LeaveRequestSegment>> GetApprovedSegmentsByEmployeeDateAsync(Guid employeeId, DateOnly leaveDate);

    Task<List<LeaveRequestSegment>> GetApprovedSegmentsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate);

    Task<LeaveRequest?> GetByIdAsync(Guid id);
    Task<List<LeaveRequest>> GetByEmployeeAsync(Guid employeeId);
    Task<List<LeaveRequest>> GetPendingAsync();
    Task<List<LeaveRequest>> GetAllAsync();
    Task AddAsync(LeaveRequest leaveRequest);
    Task UpdateAsync(LeaveRequest leaveRequest);
    Task DeleteAsync(LeaveRequest leaveRequest);
}
