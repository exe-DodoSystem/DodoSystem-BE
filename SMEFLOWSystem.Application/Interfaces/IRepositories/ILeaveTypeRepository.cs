using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ILeaveTypeRepository
{
    Task<LeaveType?> GetByIdAsync(Guid id);
    Task<LeaveType?> GetByCodeAsync(string code);
    Task<List<LeaveType>> GetAllAsync();
    Task AddAsync(LeaveType leaveType);
    Task UpdateAsync(LeaveType leaveType);
    Task DeleteAsync(LeaveType leaveType);
}
