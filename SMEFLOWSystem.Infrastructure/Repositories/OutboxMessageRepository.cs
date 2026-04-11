using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class OutboxMessageRepository : IOutboxMessageRepository
{

    private readonly SMEFLOWSystemContext _context;

    public OutboxMessageRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message)
    {
        await _context.OutboxMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task<List<OutboxMessage>> ClaimPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var candidateIds = await _context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == StatusEnum.OutboxPending
                        && (!m.NextAttemptOnUtc.HasValue || m.NextAttemptOnUtc <= utcNow))
            .OrderBy(m => m.OccurredOnUtc)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
            return new List<OutboxMessage>();

        var claimedIds = new List<Guid>(candidateIds.Count);
        foreach (var id in candidateIds)
        {
            var affected = await _context.OutboxMessages
                .Where(m => m.Id == id && m.Status == StatusEnum.OutboxPending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, StatusEnum.OutboxProcessing)
                    .SetProperty(m => m.UpdatedAt, utcNow), cancellationToken);

            if (affected == 1)
                claimedIds.Add(id);
        }

        if (claimedIds.Count == 0)
            return new List<OutboxMessage>();

        return await _context.OutboxMessages
            .Where(m => claimedIds.Contains(m.Id))
            .OrderBy(m => m.OccurredOnUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RequeueStuckProcessingAsync(DateTime staleBeforeUtc, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var affected = await _context.OutboxMessages
            .Where(m => m.Status == StatusEnum.OutboxProcessing
                        && m.UpdatedAt.HasValue
                        && m.UpdatedAt.Value <= staleBeforeUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, StatusEnum.OutboxPending)
                .SetProperty(m => m.NextAttemptOnUtc, utcNow)
                .SetProperty(m => m.UpdatedAt, utcNow), cancellationToken);

        return affected;
    }

    public async Task MarkDeadAsync(Guid id, string error, int retryCount, CancellationToken cancellationToken = default)
    {
        var msg = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (msg == null) 
            return;

        msg.Status = StatusEnum.OutboxFailed;
        msg.UpdatedAt = DateTime.UtcNow;
        msg.LastError = TrimError(error);
        msg.RetryCount = retryCount;
        msg.NextAttemptOnUtc = null;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid id, DateTime processedOnUtc, CancellationToken cancellationToken = default)
    {
        var msg = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (msg == null) return;

        msg.Status = StatusEnum.OutboxProcessed;
        msg.ProcessedOnUtc = processedOnUtc;
        msg.NextAttemptOnUtc = null;
        msg.LastError = null;
        msg.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRetryAsync(Guid id, string error, int retryCount, DateTime nextAttemptOnUtc, CancellationToken cancellationToken = default)
    {
        var msg = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (msg == null) return;

        msg.Status = StatusEnum.OutboxPending; 
        msg.RetryCount = retryCount;
        msg.NextAttemptOnUtc = nextAttemptOnUtc;
        msg.LastError = TrimError(error);
        msg.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string TrimError(string error)
    {
        if (string.IsNullOrWhiteSpace(error)) return "Unknown publish error.";
        return error.Length > 4000 ? error[..4000] : error;
    }
}
