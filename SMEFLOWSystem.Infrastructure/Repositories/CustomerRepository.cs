using SMEFLOWSystem.Application.Interfaces.IRepositories;
using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly SMEFLOWSystemContext _context;
        public CustomerRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Core.Entities.Customer customer)
        {
            await _context.Customers.AddAsync(customer);
            await _context.SaveChangesAsync();
        }

        public Task<Core.Entities.Customer?> GetInternalCustomerIgnoreTenantAsync(Guid tenantId)
        {
            return _context.Customers
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId
                            && c.Type == "Internal"
                            && (c.IsDeleted == null || c.IsDeleted == false))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}
