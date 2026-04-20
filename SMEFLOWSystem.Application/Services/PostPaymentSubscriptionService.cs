using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Events.Payments;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Services;

public class PostPaymentSubscriptionService : IPostPaymentSubscriptionService
{
    private readonly IBillingOrderRepository _billingOrderRepo;
    private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;
    private readonly IModuleSubscriptionRepository _moduleSubscriptionRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly IUserRepository _userRepo;
    private readonly IOutboxMessageRepository _outboxMessageRepo;
    private readonly ITransaction _transaction;
    private readonly IConfiguration _config;

    public PostPaymentSubscriptionService(
        IBillingOrderRepository billingOrderRepo,
        IBillingOrderModuleRepository billingOrderModuleRepo,
        IModuleSubscriptionRepository moduleSubscriptionRepo,
        ITenantRepository tenantRepo,
        IUserRepository userRepo,
        IOutboxMessageRepository outboxMessageRepo,
        ITransaction transaction,
        IConfiguration config)
    {
        _billingOrderRepo = billingOrderRepo;
        _billingOrderModuleRepo = billingOrderModuleRepo;
        _moduleSubscriptionRepo = moduleSubscriptionRepo;
        _tenantRepo = tenantRepo;
        _userRepo = userRepo;
        _outboxMessageRepo = outboxMessageRepo;
        _transaction = transaction;
        _config = config;
    }

    public async Task HandlePaymentSucceededAsync(PaymentSucceededEvent message, CancellationToken cancellationToken = default)
    {
        await _transaction.ExecuteAsync(async () =>
        {
            var order = await _billingOrderRepo.GetByIdIgnoreTenantAsync(message.BillingOrderId)
                ?? throw new KeyNotFoundException("Billing order not found.");

            if (!string.Equals(order.PaymentStatus, StatusEnum.PaymentPaid, StringComparison.OrdinalIgnoreCase))
                return;

            var tenant = await _tenantRepo.GetByIdIgnoreTenantAsync(order.TenantId)
                ?? throw new KeyNotFoundException("Tenant not found.");

            var orderModules = await _billingOrderModuleRepo.GetByBillingOrderIdIgnoreTenantAsync(order.Id);
            if (orderModules.Count == 0)
                return;

            var now = DateTime.UtcNow;
            DateTime maxEndDate = now;

            foreach (var line in orderModules)
            {
                var existingSub = await _moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenant.Id, line.ModuleId);
                if (existingSub == null)
                {
                    existingSub = new ModuleSubscription
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        ModuleId = line.ModuleId,
                        StartDate = now,
                        EndDate = now,
                        Status = StatusEnum.ModuleActive,
                        CreatedAt = now,
                        IsDeleted = false
                    };
                    await _moduleSubscriptionRepo.AddAsync(existingSub);
                }

                var baseDate = existingSub.EndDate > now ? existingSub.EndDate : now;
                existingSub.EndDate = baseDate.AddMonths(1);
                existingSub.Status = StatusEnum.ModuleActive;
                await _moduleSubscriptionRepo.UpdateIgnoreTenantAsync(existingSub);

                if (existingSub.EndDate > maxEndDate)
                    maxEndDate = existingSub.EndDate;
            }

            tenant.Status = StatusEnum.TenantActive;
            tenant.SubscriptionEndDate = DateOnly.FromDateTime(maxEndDate);
            await _tenantRepo.UpdateIgnoreTenantAsync(tenant);

            var ownerEmail = string.Empty;
            if (tenant.OwnerUserId.HasValue)
            {
                var ownerUser = await _userRepo.GetByIdIgnoreTenantAsync(tenant.OwnerUserId.Value);
                if (ownerUser != null)
                {
                    ownerUser.IsActive = true;
                    await _userRepo.UpdateUserIgnoreTenantAsync(ownerUser);
                    ownerEmail = ownerUser.Email ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(ownerEmail))
                return;

            var emailEvent = new EmailNotificationRequestedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                TenantId = tenant.Id,
                ToEmail = ownerEmail,
                Subject = $"Thanh toán thành công - Kích hoạt tài khoản SMEFLOW",
                Body = $"Chúc mừng {tenant.Name}!</h3><p>Tài khoản của bạn đã được kích hoạt thành công.</p><p>Bạn có thể đăng nhập ngay bây giờ.",
                CorrelationId = order.Id.ToString()
            };

            var exchange = _config["RabbitMQ:Exchange"] ?? "smeflow.exchange";
            var routingKey = _config["RabbitMQ:RoutingKeys:SendEmail"] ?? "email.send";

            var outboxEvent = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                EventId = emailEvent.EventId,
                EventType = nameof(EmailNotificationRequestedEvent),
                Exchange = exchange,
                RoutingKey = routingKey,
                Payload = JsonConvert.SerializeObject(emailEvent),
                CorrelationId = emailEvent.CorrelationId,
                Status = StatusEnum.OutboxPending,
                OccurredOnUtc = DateTime.UtcNow,
                NextAttemptOnUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _outboxMessageRepo.AddAsync(outboxEvent);
        });
    }
}
