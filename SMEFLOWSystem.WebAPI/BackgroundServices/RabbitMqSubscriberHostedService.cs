using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SMEFLOWSystem.Infrastructure.Messaging.Consumers;
using SMEFLOWSystem.Infrastructure.Options;

namespace SMEFLOWSystem.WebAPI.BackgroundServices;

public class RabbitMqSubscriberHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqSubscriberHostedService> _logger;
    private readonly object _channelLock = new();

    public RabbitMqSubscriberHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqSubscriberHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: _options.Durable, autoDelete: false);
                channel.BasicQos(0, _options.PrefetchCount, false);

                foreach (var kv in _options.Queues)
                {
                    var queueAlias = kv.Key;
                    var queueName = kv.Value;

                    if (!_options.RoutingKeys.TryGetValue(queueAlias, out var routingKey) || string.IsNullOrWhiteSpace(routingKey))
                    {
                        _logger.LogWarning("Skip queue {Queue} because routing key alias {Alias} is missing", queueName, queueAlias);
                        continue;
                    }

                    channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                    channel.QueueBind(queue: queueName, exchange: _options.Exchange, routingKey: routingKey);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += async (_, ea) =>
                    {
                        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var messageRoutingKey = ea.RoutingKey;

                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var handlers = scope.ServiceProvider.GetServices<IRabbitMessageHandler>();
                            var handler = handlers.FirstOrDefault(h =>
                                string.Equals(h.RoutingKey, messageRoutingKey, StringComparison.OrdinalIgnoreCase));

                            if (handler == null)
                            {
                                _logger.LogWarning("No handler found for routing key {RoutingKey}. Message acked.", messageRoutingKey);
                                lock (_channelLock)
                                {
                                    channel.BasicAck(ea.DeliveryTag, false);
                                }
                                return;
                            }

                            await handler.HandleAsync(payload, stoppingToken);

                            lock (_channelLock)
                            {
                                channel.BasicAck(ea.DeliveryTag, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error consuming message. Queue={Queue}, RoutingKey={RoutingKey}", queueName, messageRoutingKey);
                            lock (_channelLock)
                            {
                                channel.BasicNack(ea.DeliveryTag, false, true);
                            }
                        }
                    };

                    channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                    _logger.LogInformation(
                        "Subscribed queue {Queue} on exchange {Exchange} with routing key {RoutingKey}",
                        queueName, _options.Exchange, routingKey);
                }

                _logger.LogInformation("RabbitMQ subscriber started.");

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // graceful stop
                }

                _logger.LogInformation("RabbitMQ subscriber stopped.");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "RabbitMQ is not ready. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
