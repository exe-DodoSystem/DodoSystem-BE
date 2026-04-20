using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Events.Payments;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System.Text.Json;

namespace SMEFLOWSystem.Infrastructure.Messaging.Consumers;

public class PaymentSucceededConsumer : IRabbitMessageHandler
{
    private const string ConsumerName = "PaymentSucceededConsumer";

    private readonly ILogger<PaymentSucceededConsumer> _logger;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly IPostPaymentSubscriptionService _postPaymentSubscriptionService;

    public string RoutingKey => "payment.succeeded";

    public PaymentSucceededConsumer(
        ILogger<PaymentSucceededConsumer> logger,
        IProcessedEventRepository processedEventRepository,
        IPostPaymentSubscriptionService postPaymentSubscriptionService)
    {
        _logger = logger;
        _processedEventRepository = processedEventRepository;
        _postPaymentSubscriptionService = postPaymentSubscriptionService;
    }

    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Deserialize<PaymentSucceededEvent>(payload);
        if (message == null)
            throw new InvalidOperationException("Invalid PaymentSucceededEvent payload.");

        var shouldProcess = await _processedEventRepository.TryMarkProcessedAsync(
            eventId: message.EventId,
            consumerName: ConsumerName,
            cancellationToken: cancellationToken);

        if (!shouldProcess)
        {
            _logger.LogWarning(
                "Duplicate event skipped: EventId={EventId}, Consumer={Consumer}",
                message.EventId,
                ConsumerName);
            return;
        }

        await _postPaymentSubscriptionService.HandlePaymentSucceededAsync(message, cancellationToken);

        _logger.LogInformation(
            "Consumed PaymentSucceeded event: BillingOrderId={BillingOrderId}, TenantId={TenantId}, GatewayTransactionId={GatewayTransactionId}, Amount={Amount}",
            message.BillingOrderId,
            message.TenantId,
            message.GatewayTransactionId,
            message.Amount);
    }
}
