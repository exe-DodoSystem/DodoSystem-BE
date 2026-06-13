using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IModuleSubscriptionRepository
{
    Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(Guid tenantId, int moduleId);
    Task AddAsync(ModuleSubscription subscription);
    Task UpdateIgnoreTenantAsync(ModuleSubscription subscription);
    Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId);
    Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId);
    Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync();
}
