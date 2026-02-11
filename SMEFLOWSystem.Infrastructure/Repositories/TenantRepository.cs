using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class TenantRepository : ITenantRepository
    {
        private readonly SMEFLOWSystemContext _context;
        public TenantRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Core.Entities.Tenant tenant)
        {
            await _context.Tenants.AddAsync(tenant);
            await _context.SaveChangesAsync();
        }

        public async Task<Tenant?> GetByIdAsync(Guid tenantId)
        {
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted);
            return existingTenant;
        }

        public async Task<Tenant?> GetByIdIgnoreTenantAsync(Guid tenantId)
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted);
        }

        public async Task UpdateAsync(Tenant tenant)
        {
            var existingTenant = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == tenant.Id);
            if (existingTenant != null)
            {
                existingTenant.Name = tenant.Name;
                existingTenant.Status = tenant.Status;
                existingTenant.SubscriptionEndDate = tenant.SubscriptionEndDate;
                existingTenant.OwnerUserId = tenant.OwnerUserId;
                existingTenant.UpdatedAt = DateTime.UtcNow;
                _context.Tenants.Update(existingTenant);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateIgnoreTenantAsync(Tenant tenant)
        {
            var existingTenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == tenant.Id);
            if (existingTenant != null)
            {
                existingTenant.Name = tenant.Name;
                existingTenant.Status = tenant.Status;
                existingTenant.SubscriptionEndDate = tenant.SubscriptionEndDate;
                existingTenant.OwnerUserId = tenant.OwnerUserId;
                existingTenant.UpdatedAt = DateTime.UtcNow;
                _context.Tenants.Update(existingTenant);
                await _context.SaveChangesAsync();
            }
        }

        public Task<List<Tenant>> GetExpiredTenantsIgnoreTenantAsync(DateOnly todayUtc)
        {
            return _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted
                            && t.SubscriptionEndDate.HasValue
                            && t.SubscriptionEndDate.Value < todayUtc)
                .ToListAsync();
        }
    }
}
