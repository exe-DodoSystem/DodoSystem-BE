using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IRawPunchLogRepository
{
    Task AddAsync(RawPunchLog punchLog);
}