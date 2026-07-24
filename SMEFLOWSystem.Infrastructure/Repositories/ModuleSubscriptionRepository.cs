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
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ModuleId == moduleId);

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
            .AsNoTracking()
            .Include(x => x.Module)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Module!.Name)
            .ToListAsync();

    public async Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.ModuleSubscriptions
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync();
    }

    public async Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync()
    {
        return await _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .Include(x => x.Module)
            .Where(x => !x.IsDeleted)
            .ToListAsync();
    }

    public Task<ModuleSubscription?> GetByIdIgnoreTenantAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken)
        => _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .Include(subscription => subscription.Module)
            .Include(subscription => subscription.Tenant)
            .FirstOrDefaultAsync(subscription => subscription.Id == subscriptionId, cancellationToken);

    public async Task SaveSystemAdminChangesAsync(
        ModuleSubscription subscription,
        CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
