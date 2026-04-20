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
        private readonly string ConsumerName = "EmailSendConsumer";

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
            var message = JsonSerializer.Deserialize<EmailNotificationRequestedEvent>(payload);
            if (message == null)
                throw new InvalidOperationException("Invalid EmailNotificationRequestedEvent payload.");

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
        }
    }
}
