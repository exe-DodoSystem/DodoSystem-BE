using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Events.Payments;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Messaging.Consumers
{
    public class EmailSendConsumer : IRabbitMessageHandler
    {
        private const string ConsumerName = "EmailSendConsumer";

        private readonly ILogger<EmailSendConsumer> _logger;
        private readonly IProcessedEventRepository _processedEventRepository;
        private readonly IEmailService _emailService;

        public string RoutingKey => "email.send";
        public EmailSendConsumer(ILogger<EmailSendConsumer> logger, IProcessedEventRepository processedEventRepository, IEmailService emailService)
        {
            _logger = logger;
            _processedEventRepository = processedEventRepository;
            _emailService = emailService;
        }   

        public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<EmailNotificationRequestedEvent>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (message == null)
                throw new InvalidOperationException("Invalid EmailNotificationRequestedEvent payload.");

            if (string.IsNullOrWhiteSpace(message.ToEmail))
                throw new InvalidOperationException("EmailNotificationRequestedEvent.ToEmail is required.");

            if (string.IsNullOrWhiteSpace(message.Subject))
                throw new InvalidOperationException("EmailNotificationRequestedEvent.Subject is required.");

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

            await _emailService.SendEmailAsync(
                toEmail: message.ToEmail,
                subject: message.Subject,
                body: message.Body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Email event consumed successfully: EventId={EventId}, ToEmail={ToEmail}, CorrelationId={CorrelationId}",
                message.EventId,
                message.ToEmail,
                message.CorrelationId);
        }
    }
}
