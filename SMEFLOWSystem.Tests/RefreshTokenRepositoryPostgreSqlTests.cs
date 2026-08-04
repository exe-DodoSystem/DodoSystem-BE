using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Repositories;

namespace SMEFLOWSystem.Tests;

public sealed class RefreshTokenRepositoryPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task ConcurrentRotation_AllowsExactlyOneReplacement()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.ConnectionStringVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentTokenId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<SMEFLOWSystemContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = CreateContext(options, tenantId))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Refresh token integration tenant",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            setupContext.Users.Add(new User
            {
                Id = userId,
                TenantId = tenantId,
                Email = $"refresh-{userId:N}@example.test",
                PasswordHash = "unused",
                FullName = "Refresh token integration user",
                Phone = "",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            setupContext.RefreshTokens.Add(new RefreshToken
            {
                Id = currentTokenId,
                TenantId = tenantId,
                UserId = userId,
                TokenHash = $"original-{currentTokenId:N}",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await setupContext.SaveChangesAsync();
        }

        try
        {
            var revokedAt = DateTime.UtcNow;
            var results = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(async index =>
                {
                    await using var context = CreateContext(options, tenantId);
                    var repository = new RefreshTokenRepository(context);
                    return await repository.RotateAsync(
                        currentTokenId,
                        new RefreshToken
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            UserId = userId,
                            TokenHash = $"replacement-{index}-{Guid.NewGuid():N}",
                            CreatedAt = revokedAt,
                            ExpiresAt = revokedAt.AddDays(1)
                        },
                        revokedAt,
                        "Rotated");
                }));

            await using var assertionContext = CreateContext(options, tenantId);
            var tokens = await assertionContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(token => token.UserId == userId)
                .ToListAsync();
            var original = tokens.Single(token => token.Id == currentTokenId);

            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(original.RevokedAt);
            Assert.NotNull(original.ReplacedByTokenId);
            Assert.Contains(tokens, token => token.Id == original.ReplacedByTokenId);
        }
        finally
        {
            await using var cleanupContext = CreateContext(options, tenantId);
            var tokens = await cleanupContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(token => token.UserId == userId)
                .ToListAsync();
            cleanupContext.RefreshTokens.RemoveRange(tokens);

            var user = await cleanupContext.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == userId);
            if (user != null)
                cleanupContext.Users.Remove(user);

            var tenant = await cleanupContext.Tenants
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == tenantId);
            if (tenant != null)
                cleanupContext.Tenants.Remove(tenant);

            await cleanupContext.SaveChangesAsync();
        }
    }

    private static SMEFLOWSystemContext CreateContext(
        DbContextOptions<SMEFLOWSystemContext> options,
        Guid tenantId)
        => new(
            options,
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantId));
}
