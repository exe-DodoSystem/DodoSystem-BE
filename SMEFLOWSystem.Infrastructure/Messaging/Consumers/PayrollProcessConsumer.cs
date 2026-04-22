using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Events.Payments;
using SMEFLOWSystem.Application.Events.Payroll;
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
    public class PayrollProcessConsumer : IRabbitMessageHandler
    {
        private const string ConsumerName = "PayrollProcessConsumer";

        private readonly ILogger<PaymentSucceededConsumer> _logger;
        private readonly IProcessedEventRepository _processedEventRepository;
        private readonly IPayrollService _payrollService;

        public PayrollProcessConsumer(
            ILogger<PaymentSucceededConsumer> logger,
            IProcessedEventRepository processedEventRepository,
            IPayrollService payrollService)
        {
            _logger = logger;
            _processedEventRepository = processedEventRepository;
            _payrollService = payrollService;
        }

        public string RoutingKey => "payroll.process";

        public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<PayrollProcessEvent>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

            await _payrollService.GenerateMonthlyPayrollAsync(message.TenantId, message.Month, message.Year);

            _logger.LogInformation(
                "Consumed PayrollProcess event: TenantId={TenantId}, Month={Month}, Year={Year}",
                message.TenantId,
                message.Month,
                message.Year);
        }
    }
}
