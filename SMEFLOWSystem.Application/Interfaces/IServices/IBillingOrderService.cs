using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IBillingOrderService
{
    Task<BillingOrder> CreateSubscriptionBillingOrderAsync(Guid tenantId, Guid customerId, int subscriptionPlanId);
}
