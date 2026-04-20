namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IProcessedEventRepository
{
    Task<bool> TryMarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}
