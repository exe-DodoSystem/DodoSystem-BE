using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class BillingOrderModuleRepository : IBillingOrderModuleRepository
{
    private readonly SMEFLOWSystemContext _context;

    public BillingOrderModuleRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<BillingOrderModule> items)
    {
        await _context.BillingOrderModules.AddRangeAsync(items);
        await _context.SaveChangesAsync();
    }

    public Task<List<BillingOrderModule>> GetByBillingOrderIdIgnoreTenantAsync(Guid billingOrderId)
        => _context.BillingOrderModules
            .IgnoreQueryFilters()
            .Where(x => x.BillingOrderId == billingOrderId)
            .ToListAsync();

    public Task<List<BillingOrderModule>> GetByTenantAndModuleAsync(Guid tenantId, int moduleId)
        => _context.BillingOrderModules
            .Include(x => x.BillingOrder)
            .Where(x => x.ModuleId == moduleId
                        && x.BillingOrder != null
                        && x.BillingOrder.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
}
