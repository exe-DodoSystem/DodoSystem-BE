using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SMEFLOWSystemContext _context;

    public RefreshTokenRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token)
    {
        await _context.RefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RotateAsync(
        Guid currentTokenId,
        RefreshToken replacementToken,
        DateTime revokedAt,
        string reason)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.RefreshTokens.AddAsync(replacementToken);
                await _context.SaveChangesAsync();

                var affectedRows = await _context.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(token => token.Id == currentTokenId
                        && token.RevokedAt == null
                        && token.ExpiresAt > revokedAt)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(token => token.RevokedAt, revokedAt)
                        .SetProperty(token => token.ReplacedByTokenId, replacementToken.Id)
                        .SetProperty(token => token.RevokeReason, reason));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    _context.Entry(replacementToken).State = EntityState.Detached;
                    return false;
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                _context.Entry(replacementToken).State = EntityState.Detached;
                throw;
            }
        });
    }

    public Task<RefreshToken?> GetByTokenHashIgnoreTenantAsync(string tokenHash)
    {
        return _context.RefreshTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public Task<List<RefreshToken>> GetByUserIdAsync(Guid userId, Guid tenantId)
    {
        return _context.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task RevokeAllAsync(Guid userId, Guid tenantId, string reason)
    {
        var now = DateTime.UtcNow;

        await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(token => token.UserId == userId
                && token.TenantId == tenantId
                && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.RevokeReason, reason));
    }
}
