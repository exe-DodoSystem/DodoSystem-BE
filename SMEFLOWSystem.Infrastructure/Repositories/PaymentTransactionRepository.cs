using Microsoft.EntityFrameworkCore;
using Npgsql;
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
            return ex.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
        }
    }
}
