using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<bool> RotateAsync(
        Guid currentTokenId,
        RefreshToken replacementToken,
        DateTime revokedAt,
        string reason);
    Task<RefreshToken?> GetByTokenHashIgnoreTenantAsync(string tokenHash);
    Task<List<RefreshToken>> GetByUserIdAsync(Guid userId, Guid tenantId);
    Task RevokeAllAsync(Guid userId, Guid tenantId, string reason);
}
