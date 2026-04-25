using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public PaymentTransactionRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task<PaymentTransaction?> GetByGatewayTransactionIdAsync(string gateway, string gatewayTransactionId, bool ignoreTenantFilter = false)
        {
            var query = _context.PaymentTransactions.AsQueryable();
            if (ignoreTenantFilter)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query.FirstOrDefaultAsync(x => x.Gateway == gateway && x.GatewayTransactionId == gatewayTransactionId);
        }

        public async Task AddAsync(PaymentTransaction transaction)
        {
            await _context.PaymentTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> TryAddAsync(PaymentTransaction transaction)
        {
            await _context.PaymentTransactions.AddAsync(transaction);
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Duplicate callback / retry: treat as idempotent success.
                _context.Entry(transaction).State = EntityState.Detached;
                return false;
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // SQL Server duplicate key: 2601 (unique index) / 2627 (unique constraint)
            return ex.InnerException is SqlException sqlEx
                && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }
    }
}
