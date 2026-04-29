using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ILeaveRequestRepository
{
    /// <summary>
    /// Lấy tất cả LeaveRequestSegment đã được duyệt (Approved) cho một nhân viên trong một ngày cụ thể.
    /// Join qua LeaveRequest để filter theo Status = "Approved".
    /// </summary>
    Task<List<LeaveRequestSegment>> GetApprovedSegmentsByEmployeeDateAsync(Guid employeeId, DateOnly leaveDate);
}
