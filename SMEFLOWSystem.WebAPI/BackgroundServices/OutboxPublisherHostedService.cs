using SMEFLOWSystem.Application.Abstractions.Messaging;
using SMEFLOWSystem.Application.Events.Attendance;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Events.Payments;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
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
        private const int MaxParallelism = 4;

        private long _processed;
        private long _retried;
        private long _dead;
        public OutboxPublisherHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxPublisherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private async Task PublishTypedAsync(IEventPublisher publisher, OutboxMessage msg, CancellationToken token)
        {
            switch (msg.EventType)
            {
                case nameof(PaymentSucceededEvent):
                    {
                        var evt = JsonSerializer.Deserialize<PaymentSucceededEvent>(msg.Payload)
                                  ?? throw new InvalidOperationException("Invalid payload for PaymentSucceededEvent");
                        await publisher.PublishAsync(msg.RoutingKey, evt, token);
                        break;
                    }

                case nameof(AttendanceApprovedEvent):
                    {
                        var evt = JsonSerializer.Deserialize<AttendanceApprovedEvent>(msg.Payload)
                                  ?? throw new InvalidOperationException("Invalid payload for AttendanceApprovedEvent");
                        await publisher.PublishAsync(msg.RoutingKey, evt, token);
                        break;
                    }
                case nameof(EmailNotificationRequestedEvent):
                    {
                        var evt = JsonSerializer.Deserialize<EmailNotificationRequestedEvent>(msg.Payload)
                                  ?? throw new InvalidOperationException("Invalid payload for EmailNotificationRequestedEvent");
                        await publisher.PublishAsync(msg.RoutingKey, evt, token);
                        break;
                    }

                default:
                    throw new InvalidOperationException($"Unsupported event type: {msg.EventType}");
            }
        }

        private async Task ProcessOneAsync(OutboxMessage msg,IOutboxMessageRepository outboxRepo, IEventPublisher publisher, CancellationToken stoppingToken)
        {
            try
            {
                await PublishTypedAsync(publisher, msg, stoppingToken);

                await outboxRepo.MarkProcessedAsync(
                    id: msg.Id,
                    processedOnUtc: DateTime.UtcNow,
                    cancellationToken: stoppingToken);

                Interlocked.Increment(ref _processed);

                _logger.LogDebug(
                    "Outbox processed: Id={OutboxId}, EventType={EventType}, RoutingKey={RoutingKey}, RetryCount={RetryCount}, CorrelationId={CorrelationId}",
                    msg.Id, msg.EventType, msg.RoutingKey, msg.RetryCount, msg.CorrelationId);
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

                    Interlocked.Increment(ref _dead);

                    _logger.LogError(
                        ex,
                        "Outbox moved to dead: Id={OutboxId}, EventType={EventType}, RoutingKey={RoutingKey}, RetryCount={RetryCount}, CorrelationId={CorrelationId}",
                        msg.Id, msg.EventType, msg.RoutingKey, nextRetryCount, msg.CorrelationId);
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

                    Interlocked.Increment(ref _retried);

                    _logger.LogWarning(
                        ex,
                        "Outbox retry scheduled: Id={OutboxId}, EventType={EventType}, RoutingKey={RoutingKey}, RetryCount={RetryCount}, NextAttempt={NextAttempt}, CorrelationId={CorrelationId}",
                        msg.Id, msg.EventType, msg.RoutingKey, nextRetryCount, nextAttempt, msg.CorrelationId);
                }
            }
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

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = MaxParallelism,
                        CancellationToken = stoppingToken
                    };

                    await Parallel.ForEachAsync(messages, parallelOptions, async (msg, ct) =>
                    {
                        using var innerScope = _scopeFactory.CreateScope();
                        var scopedRepo = innerScope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                        var scopedPublisher = innerScope.ServiceProvider.GetRequiredService<IEventPublisher>();

                        await ProcessOneAsync(msg, scopedRepo, scopedPublisher, ct);
                    });

                    _logger.LogDebug(
                        "Outbox metrics: Processed={Processed}, Retried={Retried}, Dead={Dead}",
                        Interlocked.Read(ref _processed),
                        Interlocked.Read(ref _retried),
                        Interlocked.Read(ref _dead));
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
