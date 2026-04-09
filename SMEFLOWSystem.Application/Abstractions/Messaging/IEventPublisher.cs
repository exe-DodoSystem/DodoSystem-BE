namespace SMEFLOWSystem.Application.Abstractions.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string routingKey, TEvent message, CancellationToken cancellationToken = default);
    Task PublishByNameAsync<TEvent>(string routingKeyName, TEvent message, CancellationToken cancellationToken = default);
}
