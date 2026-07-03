using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IBillingOrderRepository
    {
        Task<BillingOrder?> GetByIdAsync(Guid billingOrderId);
        Task<List<BillingOrder>> GetByTenantIdAsync(Guid tenantId);
        Task<BillingOrder?> GetByIdIgnoreTenantAsync(Guid billingOrderId);
        Task<BillingOrder?> GetByOrderNumberIgnoreTenantAsync(string billingOrderNumber);
        Task AddAsync(BillingOrder billingOrder);
        Task<BillingOrder?> UpdateAsync(BillingOrder billingOrder);
        Task<BillingOrder?> UpdateIgnoreTenantAsync(BillingOrder billingOrder);
    }
}
