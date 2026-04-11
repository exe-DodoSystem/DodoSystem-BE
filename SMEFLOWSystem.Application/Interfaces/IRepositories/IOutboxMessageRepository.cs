using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message);
    Task<List<OutboxMessage>> ClaimPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<int> RequeueStuckProcessingAsync(DateTime staleBeforeUtc, DateTime utcNow, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid id, DateTime processedOnUtc, CancellationToken cancellationToken = default);
    Task MarkDeadAsync( Guid id, string error,int retryCount, CancellationToken cancellationToken = default);
    Task MarkRetryAsync(Guid id, string error, int retryCount, DateTime nextAttemptOnUtc, CancellationToken cancellationToken = default);
}
