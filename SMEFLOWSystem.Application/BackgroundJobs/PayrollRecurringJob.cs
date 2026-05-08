using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Events.Payroll;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.BackgroundJobs
{
    public class PayrollRecurringJob
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IOutboxMessageRepository _outboxMessageRepository;
        private readonly IConfiguration _configuration;

        public PayrollRecurringJob(
            ITenantRepository tenantRepository,
            IOutboxMessageRepository outboxMessageRepository,
            IConfiguration configuration)
        {
            _tenantRepository = tenantRepository;
            _outboxMessageRepository = outboxMessageRepository;
            _configuration = configuration;
        }

        public async Task GeneratePayrollForAllTenant()
        {
            var payrollPeriod = DateTime.UtcNow.AddMonths(-1);

            var tenants = await _tenantRepository.GetAllIgnoreTenantAsync();

            foreach (var tenant in tenants)
            {
                if(tenant.IsDeleted)
                    continue;

                var payrollEvent = new PayrollProcessEvent
                {
                    EventId = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow,
                    TenantId = tenant.Id,
                    Month = payrollPeriod.Month,
                    CorrelationId = tenant.Id.ToString(),
                    Year = payrollPeriod.Year
                };

                var exchange = _configuration["RabbitMQ:Exchange"] ?? "smeflow.exchange";
                var routingKey = _configuration["RabbitMQ:RoutingKeys:Payroll"] ?? "payroll.process";

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    EventId = payrollEvent.EventId,
                    EventType = nameof(PayrollProcessEvent),
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Payload = JsonConvert.SerializeObject(payrollEvent),
                    CorrelationId = payrollEvent.CorrelationId,
                    Status = StatusEnum.OutboxPending,
                    OccurredOnUtc = DateTime.UtcNow,
                    NextAttemptOnUtc = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await _outboxMessageRepository.AddAsync(outbox);
            }
        }
    }
}
