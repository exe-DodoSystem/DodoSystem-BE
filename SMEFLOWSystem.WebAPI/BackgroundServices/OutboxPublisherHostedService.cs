using SMEFLOWSystem.Application.Abstractions.Messaging;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using System.Text.Json;

namespace SMEFLOWSystem.WebAPI.BackgroundServices
{
    public class OutboxPublisherHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxPublisherHostedService> _logger;
        private const int BatchSize = 50;
        private const int PollSeconds = 5;
        private const int MaxRetryCount = 10;
        private const int StaleProcessingMinutes = 2;

        public OutboxPublisherHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxPublisherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox publisher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                    var now = DateTime.UtcNow;

                    var requeued = await outboxRepo.RequeueStuckProcessingAsync(
                        staleBeforeUtc: now.AddMinutes(-StaleProcessingMinutes),
                        utcNow: now,
                        cancellationToken: stoppingToken);

                    if (requeued > 0)
                        _logger.LogWarning("Requeued {Count} stuck outbox messages", requeued);

                    var messages = await outboxRepo.ClaimPendingBatchAsync(
                        batchSize: BatchSize,
                        utcNow: now,
                        cancellationToken: stoppingToken);

                    foreach (var msg in messages)
                    {
                        try
                        {
                            var payload = JsonSerializer.Deserialize<JsonElement>(msg.Payload);

                            await publisher.PublishAsync(msg.RoutingKey, payload, stoppingToken);

                            await outboxRepo.MarkProcessedAsync(
                                id: msg.Id,
                                processedOnUtc: DateTime.UtcNow,
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            var nextRetryCount = msg.RetryCount + 1;

                            if (nextRetryCount >= MaxRetryCount)
                            {
                                await outboxRepo.MarkDeadAsync(
                                    id: msg.Id,
                                    error: ex.ToString(),
                                    retryCount: nextRetryCount,
                                    cancellationToken: stoppingToken);

                                _logger.LogError(ex, "Outbox message {OutboxId} moved to dead state", msg.Id);
                            }
                            else
                            {
                                var delaySec = Math.Min(300, 5 * (int)Math.Pow(2, Math.Min(nextRetryCount, 6)));
                                var nextAttempt = DateTime.UtcNow.AddSeconds(delaySec);

                                await outboxRepo.MarkRetryAsync(
                                    id: msg.Id,
                                    error: ex.Message,
                                    retryCount: nextRetryCount,
                                    nextAttemptOnUtc: nextAttempt,
                                    cancellationToken: stoppingToken);

                                _logger.LogWarning(ex,
                                    "Outbox message {OutboxId} retry {RetryCount}, next at {NextAttempt}",
                                    msg.Id, nextRetryCount, nextAttempt);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox publisher loop failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(PollSeconds), stoppingToken);
            }

            _logger.LogInformation("Outbox publisher stopped");
        }
    }
}
