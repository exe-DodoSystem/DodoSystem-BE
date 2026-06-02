using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IOvertimeRequestRepository
{
    
    Task<OvertimeRequest?> GetApprovedByEmployeeDateAsync(Guid employeeId, DateOnly overtimeDate);

    Task<List<OvertimeRequest>> GetApprovedRequestsForEmployeesAsync(List<Guid> employeeIds, DateOnly minDate, DateOnly maxDate);
}
