using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface ITenantRepository
    {
        Task AddAsync(Tenant tenant);
        Task UpdateAsync(Tenant tenant);
        Task<Tenant?> GetByIdAsync(Guid tenantId);
        Task<Tenant?> GetByIdIgnoreTenantAsync(Guid tenantId);
        Task UpdateIgnoreTenantAsync(Tenant tenant);
        Task<List<Tenant>> GetExpiredTenantsIgnoreTenantAsync(DateOnly todayUtc);
        Task<List<Tenant>> GetAllIgnoreTenantAsync();
        Task<Tenant?> GetByOwnerUserIdIgnoreAsync(Guid ownerId);

        Task<(List<Tenant> Items, int TotalCount)> GetPagedIgnoreTenantAsync(int pageNumber, int pageSize);
        Task UpdateStatusIgnoreTenantAsync(
            Guid tenantId,
            string status,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken);
    }
}
