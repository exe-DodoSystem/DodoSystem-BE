using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SMEFLOWSystem.Application.Abstractions.Messaging;
using SMEFLOWSystem.Infrastructure.Options;

namespace SMEFLOWSystem.Infrastructure.Messaging.RabbitMQ;

public class RabbitMqEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(string routingKey, TEvent message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routingKey))
            throw new ArgumentException("Routing key is required.", nameof(routingKey));

        PublishInternal(routingKey.Trim(), message);
        return Task.CompletedTask;
    }

    public Task PublishByNameAsync<TEvent>(string routingKeyName, TEvent message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routingKeyName))
            throw new ArgumentException("Routing key name is required.", nameof(routingKeyName));

        if (!_options.RoutingKeys.TryGetValue(routingKeyName, out var routingKey) || string.IsNullOrWhiteSpace(routingKey))
            throw new InvalidOperationException($"RabbitMQ routing key '{routingKeyName}' is not configured.");

        PublishInternal(routingKey, message);
        return Task.CompletedTask;
    }

    private void PublishInternal<TEvent>(string routingKey, TEvent message)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            RequestedHeartbeat = TimeSpan.FromSeconds(_options.RequestedHeartbeat),
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds)
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange: _options.Exchange,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: false,
            arguments: null);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));

        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        channel.BasicPublish(
            exchange: _options.Exchange,
            routingKey: routingKey,
            basicProperties: props,
            body: body);

        _logger.LogInformation("Published event {EventType} to exchange {Exchange} with routing key {RoutingKey}", typeof(TEvent).Name, _options.Exchange, routingKey);
    }
}
