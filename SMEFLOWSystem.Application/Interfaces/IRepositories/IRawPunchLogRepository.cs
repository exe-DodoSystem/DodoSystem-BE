using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IRawPunchLogRepository
{
    Task AddAsync(RawPunchLog punchLog);
    Task<List<RawPunchLog>> GetUnprocessedBatchAsync(int batchSize);
    Task MarkProcessedAsync(IEnumerable<Guid> punchLogIds);
}