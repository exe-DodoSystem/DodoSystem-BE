using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Services;

public class BillingOrderService : IBillingOrderService
{
    private readonly IBillingOrderRepository _billingOrderRepo;
    private readonly ISubscriptionPlanRepository _planRepo;

    public BillingOrderService(IBillingOrderRepository billingOrderRepo, ISubscriptionPlanRepository planRepo)
    {
        _billingOrderRepo = billingOrderRepo;
        _planRepo = planRepo;
    }

    public async Task<BillingOrder> CreateSubscriptionBillingOrderAsync(Guid tenantId, Guid customerId, int subscriptionPlanId)
    {
        var plan = await _planRepo.GetByIdAsync(subscriptionPlanId);
        if (plan == null) throw new Exception("Gói dịch vụ không tồn tại!");

        var billingOrder = new BillingOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            BillingOrderNumber = AuthHelper.GenerateOrderNumber(),
            BillingDate = DateTime.UtcNow,
            Status = StatusEnum.OrderPending,
            PaymentStatus = StatusEnum.PaymentPending,
            TotalAmount = plan.Price,
            DiscountAmount = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _billingOrderRepo.AddAsync(billingOrder);
        return billingOrder;
    }
}
