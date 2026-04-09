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
}
