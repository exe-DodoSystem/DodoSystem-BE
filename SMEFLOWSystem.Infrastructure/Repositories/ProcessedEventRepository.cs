using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ProcessedEventRepository : IProcessedEventRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ProcessedEventRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<bool> TryMarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        var row = new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ConsumerName = consumerName,
            ProcessedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ProcessedEvents.AddAsync(row, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _context.Entry(row).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
               && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
