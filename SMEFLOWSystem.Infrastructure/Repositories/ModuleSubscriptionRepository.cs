using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ModuleSubscriptionRepository : IModuleSubscriptionRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ModuleSubscriptionRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(Guid tenantId, int moduleId)
        => _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ModuleId == moduleId && !x.IsDeleted);

    public async Task AddAsync(ModuleSubscription subscription)
    {
        await _context.ModuleSubscriptions.AddAsync(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateIgnoreTenantAsync(ModuleSubscription subscription)
    {
        var existing = await _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == subscription.Id);
        if (existing == null) return;

        existing.Status = subscription.Status;
        existing.StartDate = subscription.StartDate;
        existing.EndDate = subscription.EndDate;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.IsDeleted = subscription.IsDeleted;

        await _context.SaveChangesAsync();
    }

    public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId)
        => _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync();
}
