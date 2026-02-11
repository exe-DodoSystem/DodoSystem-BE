using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IBillingOrderModuleRepository
{
    Task AddRangeAsync(IEnumerable<BillingOrderModule> items);
    Task<List<BillingOrderModule>> GetByBillingOrderIdIgnoreTenantAsync(Guid billingOrderId);
    Task<List<BillingOrderModule>> GetByTenantAndModuleAsync(Guid tenantId, int moduleId);
}
