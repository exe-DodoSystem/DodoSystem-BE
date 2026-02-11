using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IBillingOrderService
{
    Task<BillingOrder> CreateModuleBillingOrderAsync(
        Guid tenantId,
        Guid customerId,
        IReadOnlyCollection<int> moduleIds,
        bool isTrialOrder = false,
        DateTime? prorateUntilUtc = null);
}
