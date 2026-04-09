using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message);
}
