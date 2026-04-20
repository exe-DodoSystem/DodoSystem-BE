namespace SMEFLOWSystem.Infrastructure.Messaging.Consumers;

public interface IRabbitMessageHandler
{
    string RoutingKey { get; }
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
