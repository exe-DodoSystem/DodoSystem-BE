using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class RawPunchLogRepository : IRawPunchLogRepository
{
    private readonly SMEFLOWSystemContext _context;

    public RawPunchLogRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RawPunchLog punchLog)
    {
        await _context.RawPunchLogs.AddAsync(punchLog);
        await _context.SaveChangesAsync();
    }

    public async Task<(RawPunchLog PunchLog, bool IsNew)> AddIdempotentAsync(
        RawPunchLog punchLog)
    {
        if (punchLog.ClientRequestId == null)
        {
            await AddAsync(punchLog);
            return (punchLog, true);
        }

        try
        {
            await _context.RawPunchLogs.AddAsync(punchLog);
            await _context.SaveChangesAsync();
            return (punchLog, true);
        }
        catch (DbUpdateException exception)
            when (IsIdempotencyConstraintViolation(exception))
        {
            _context.Entry(punchLog).State = EntityState.Detached;

            var existing = await GetByClientRequestIdAsync(
                punchLog.TenantId,
                punchLog.EmployeeId,
                punchLog.ClientRequestId);

            if (existing == null)
            {
                throw;
            }

            return (existing, false);
        }
    }

    public Task<RawPunchLog?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid employeeId,
        string clientRequestId)
    {
        return _context.RawPunchLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(log =>
                log.TenantId == tenantId &&
                log.EmployeeId == employeeId &&
                log.ClientRequestId == clientRequestId);
    }

    public Task<RawPunchLog?> GetLatestByEmployeePunchTypeAsync(
        Guid tenantId,
        Guid employeeId,
        string punchType,
        DateTime sinceUtc)
    {
        return _context.RawPunchLogs
            .AsNoTracking()
            .Where(log =>
                log.TenantId == tenantId &&
                log.EmployeeId == employeeId &&
                log.PunchType == punchType &&
                log.Timestamp >= sinceUtc)
            .OrderByDescending(log => log.Timestamp)
            .FirstOrDefaultAsync();
    }

    private static bool IsIdempotencyConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                RawPunchLogConfiguration.IdempotencyIndexName
        };
    }

    public async Task<List<RawPunchLog>> GetByEmployeeAndDateRangeAsync(Guid employeeId, DateTime fromDate, DateTime toDate)
    {
        return await _context.RawPunchLogs
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.Timestamp >= fromDate && x.Timestamp <= toDate)
            .OrderBy(x  => x.Timestamp)
            .ToListAsync();
    }

    public async Task<List<RawPunchLog>> GetUnprocessedBatchAsync(int batchSize)
    {
        if (batchSize <= 0) batchSize = 500;

        return await _context.RawPunchLogs
            .AsNoTracking()
            .Where(x => !x.IsProcessed)
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task MarkProcessedAsync(IEnumerable<Guid> punchLogIds)
    {
        var ids = punchLogIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            return;

        await _context.RawPunchLogs
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsProcessed, true));
    }

    public async Task MarkUnprocessedForRecalculateAsync(Guid employeeId, DateTime fromDate, DateTime toDate)
    {
        await _context.RawPunchLogs
            .Where(x => x.EmployeeId == employeeId && x.Timestamp >= fromDate && x.Timestamp <= toDate)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsProcessed, false));
    }

    public async Task IncrementRetryCountAsync(IEnumerable<Guid> logIds)
    {
        var ids = logIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0) return;

        await _context.RawPunchLogs
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RetryCount, p => p.RetryCount + 1));
    }
}
