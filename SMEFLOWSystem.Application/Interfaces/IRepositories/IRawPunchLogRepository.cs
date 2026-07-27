using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IRawPunchLogRepository
{
    Task AddAsync(RawPunchLog punchLog);
    Task<(RawPunchLog PunchLog, bool IsNew)> AddIdempotentAsync(
        RawPunchLog punchLog);
    Task<RawPunchLog?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid employeeId,
        string clientRequestId);
    Task<RawPunchLog?> GetLatestByEmployeePunchTypeAsync(
        Guid tenantId,
        Guid employeeId,
        string punchType,
        DateTime sinceUtc);
    Task<List<RawPunchLog>> GetUnprocessedBatchAsync(int batchSize);
    Task MarkProcessedAsync(IEnumerable<Guid> punchLogIds);
    Task MarkUnprocessedForRecalculateAsync(Guid employeeId, DateTime fromDate, DateTime toDate);
    Task<List<RawPunchLog>> GetByEmployeeAndDateRangeAsync(Guid employeeId, DateTime fromDate, DateTime toDate);
    Task IncrementRetryCountAsync(IEnumerable<Guid> logIds);
}
