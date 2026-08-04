using Microsoft.Extensions.Configuration;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.RefreshTokenDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace SMEFLOWSystem.Tests;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task IssueAsync_UsesConfiguredLifetimesAndCurrentModuleState()
    {
        var user = CreateUser();
        var refreshTokens = new FakeRefreshTokenRepository();
        var service = CreateService(user, refreshTokens, []);
        var beforeIssue = DateTime.UtcNow;

        var result = await service.IssueAsync(user.Id);

        Assert.True(result.IsExpired);
        Assert.Equal("true", ReadClaim(result.AccessToken, "isExpired"));
        Assert.NotEqual(result.RefreshToken, refreshTokens.Tokens.Single().TokenHash);
        Assert.InRange(
            refreshTokens.Tokens.Single().ExpiresAt,
            beforeIssue.AddDays(3),
            DateTime.UtcNow.AddDays(3).AddSeconds(2));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.InRange(
            jwt.ValidTo,
            beforeIssue.AddMinutes(12).AddSeconds(-1),
            DateTime.UtcNow.AddMinutes(12).AddSeconds(2));
    }

    [Fact]
    public async Task RefreshAsync_RecomputesModuleStateAndRotatesToken()
    {
        var user = CreateUser();
        var refreshTokens = new FakeRefreshTokenRepository();
        var subscriptions = new List<ModuleSubscription>();
        var service = CreateService(user, refreshTokens, subscriptions);
        var issued = await service.IssueAsync(user.Id);
        subscriptions.Add(CreateActiveSubscription(user.TenantId));

        var (success, response, message) = await service.RefreshAsync(new RefreshRequestDto
        {
            RefreshToken = issued.RefreshToken
        });

        Assert.True(success, message);
        Assert.NotNull(response);
        Assert.False(response.IsExpired);
        Assert.Equal("false", ReadClaim(response.AccessToken, "isExpired"));
        Assert.Equal(2, refreshTokens.Tokens.Count);
        Assert.NotNull(refreshTokens.Tokens[0].RevokedAt);
        Assert.Equal(refreshTokens.Tokens[1].Id, refreshTokens.Tokens[0].ReplacedByTokenId);
        Assert.Equal("Rotated", refreshTokens.Tokens[0].RevokeReason);
    }

    [Fact]
    public async Task RefreshAsync_WhenRotatedTokenIsReused_RevokesAllActiveTokens()
    {
        var user = CreateUser();
        var refreshTokens = new FakeRefreshTokenRepository();
        var service = CreateService(
            user,
            refreshTokens,
            [CreateActiveSubscription(user.TenantId)]);
        var issued = await service.IssueAsync(user.Id);
        var firstRefresh = await service.RefreshAsync(new RefreshRequestDto
        {
            RefreshToken = issued.RefreshToken
        });
        Assert.True(firstRefresh.success);

        var reused = await service.RefreshAsync(new RefreshRequestDto
        {
            RefreshToken = issued.RefreshToken
        });

        Assert.False(reused.success);
        Assert.Null(reused.response);
        Assert.Equal(1, refreshTokens.RevokeAllCalls);
        Assert.All(refreshTokens.Tokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(
            "Refresh token reuse detected",
            refreshTokens.Tokens.Single(token => token.ReplacedByTokenId == null).RevokeReason);
    }

    [Fact]
    public async Task RefreshAsync_WhenUserBecomesInactive_RejectsAndRevokesSession()
    {
        var user = CreateUser();
        var refreshTokens = new FakeRefreshTokenRepository();
        var service = CreateService(
            user,
            refreshTokens,
            [CreateActiveSubscription(user.TenantId)]);
        var issued = await service.IssueAsync(user.Id);
        user.IsActive = false;

        var result = await service.RefreshAsync(new RefreshRequestDto
        {
            RefreshToken = issued.RefreshToken
        });

        Assert.False(result.success);
        Assert.Contains("không còn khả dụng", result.message);
        Assert.Equal(1, refreshTokens.RevokeAllCalls);
        Assert.NotNull(refreshTokens.Tokens.Single().RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_WhenAtomicRotationLosesRace_RevokesSession()
    {
        var user = CreateUser();
        var refreshTokens = new FakeRefreshTokenRepository
        {
            ForceRotationFailure = true
        };
        var service = CreateService(
            user,
            refreshTokens,
            [CreateActiveSubscription(user.TenantId)]);
        var issued = await service.IssueAsync(user.Id);

        var result = await service.RefreshAsync(new RefreshRequestDto
        {
            RefreshToken = issued.RefreshToken
        });

        Assert.False(result.success);
        Assert.Equal("RefreshToken đã được sử dụng", result.message);
        Assert.Single(refreshTokens.Tokens);
        Assert.Equal(1, refreshTokens.RevokeAllCalls);
    }

    [Fact]
    public async Task IssueAsync_ForSystemAdmin_DoesNotRequireModuleSubscription()
    {
        var user = CreateUser();
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Role = new Role { Name = "SystemAdmin" }
        });
        var service = CreateService(user, new FakeRefreshTokenRepository(), []);

        var result = await service.IssueAsync(user.Id);

        Assert.False(result.IsExpired);
        Assert.Equal("false", ReadClaim(result.AccessToken, "isExpired"));
    }

    private static RefreshTokenService CreateService(
        User user,
        FakeRefreshTokenRepository refreshTokens,
        List<ModuleSubscription> subscriptions)
    {
        var configuration = new ConfigurationManager
        {
            ["Jwt:Issuer"] = "SMEFLOW.Tests",
            ["Jwt:Audience"] = "SMEFLOW.Tests.Client",
            ["Jwt:Secret"] = "refresh-token-tests-secret-key-at-least-32-characters",
            ["Jwt:AccessTokenExpiryMinutes"] = "12",
            ["Jwt:RefreshTokenExpiryDays"] = "3"
        };

        return new RefreshTokenService(
            refreshTokens,
            new FakeUserRepository(user),
            new FakeModuleSubscriptionRepository(subscriptions),
            configuration);
    }

    private static User CreateUser()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test tenant",
            Status = StatusEnum.TenantActive,
            IsDeleted = false
        };

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Tenant = tenant,
            Email = "user@example.com",
            FullName = "Test User",
            PasswordHash = "unused",
            Phone = "",
            IsActive = true,
            IsDeleted = false
        };
    }

    private static ModuleSubscription CreateActiveSubscription(Guid tenantId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = 1,
            Status = StatusEnum.ModuleActive,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

    private static string ReadClaim(string accessToken, string claimType)
        => new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken)
            .Claims
            .Single(claim => claim.Type == claimType)
            .Value;

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly object _sync = new();

        public List<RefreshToken> Tokens { get; } = [];
        public int RevokeAllCalls { get; private set; }
        public bool ForceRotationFailure { get; init; }

        public Task AddAsync(RefreshToken token)
        {
            lock (_sync)
            {
                Tokens.Add(token);
            }

            return Task.CompletedTask;
        }

        public Task<bool> RotateAsync(
            Guid currentTokenId,
            RefreshToken replacementToken,
            DateTime revokedAt,
            string reason)
        {
            lock (_sync)
            {
                var current = Tokens.FirstOrDefault(token =>
                    token.Id == currentTokenId
                    && token.RevokedAt == null
                    && token.ExpiresAt > revokedAt);
                if (ForceRotationFailure || current == null)
                    return Task.FromResult(false);

                Tokens.Add(replacementToken);
                current.RevokedAt = revokedAt;
                current.ReplacedByTokenId = replacementToken.Id;
                current.RevokeReason = reason;
                return Task.FromResult(true);
            }
        }

        public Task<RefreshToken?> GetByTokenHashIgnoreTenantAsync(string tokenHash)
        {
            lock (_sync)
            {
                return Task.FromResult(Tokens.FirstOrDefault(token =>
                    token.TokenHash == tokenHash));
            }
        }

        public Task<List<RefreshToken>> GetByUserIdAsync(Guid userId, Guid tenantId)
        {
            lock (_sync)
            {
                return Task.FromResult(Tokens
                    .Where(token => token.UserId == userId && token.TenantId == tenantId)
                    .ToList());
            }
        }

        public Task RevokeAllAsync(Guid userId, Guid tenantId, string reason)
        {
            lock (_sync)
            {
                RevokeAllCalls++;
                var now = DateTime.UtcNow;
                foreach (var token in Tokens.Where(token =>
                    token.UserId == userId
                    && token.TenantId == tenantId
                    && token.RevokedAt == null))
                {
                    token.RevokedAt = now;
                    token.RevokeReason = reason;
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeModuleSubscriptionRepository(
        List<ModuleSubscription> subscriptions) : IModuleSubscriptionRepository
    {
        public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId)
            => Task.FromResult(subscriptions
                .Where(subscription => subscription.TenantId == tenantId)
                .ToList());

        public Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(
            Guid tenantId,
            int moduleId)
            => Task.FromResult(subscriptions.FirstOrDefault(subscription =>
                subscription.TenantId == tenantId && subscription.ModuleId == moduleId));

        public Task AddAsync(ModuleSubscription subscription)
        {
            subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public Task UpdateIgnoreTenantAsync(ModuleSubscription subscription)
            => Task.CompletedTask;

        public Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId)
            => GetByTenantIgnoreTenantAsync(tenantId);

        public Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync()
            => Task.FromResult(subscriptions.ToList());

        public Task<ModuleSubscription?> GetByIdIgnoreTenantAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken)
            => Task.FromResult(subscriptions.FirstOrDefault(subscription =>
                subscription.Id == subscriptionId));

        public Task SaveSystemAdminChangesAsync(
            ModuleSubscription subscription,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdIgnoreTenantAsync(Guid id)
            => Task.FromResult<User?>(id == user.Id ? user : null);

        public Task<User?> GetUserByIdAsync(Guid id)
            => GetByIdIgnoreTenantAsync(id);

        public Task<User?> GetUserByEmailAsync(string email)
            => Task.FromResult<User?>(user.Email == email ? user : null);

        public Task<User?> AddUserAsync(User newUser) => Task.FromResult<User?>(newUser);
        public Task<bool> IsEmailExistAsync(string email) => Task.FromResult(user.Email == email);
        public Task<List<User>> GetUserByNameAsync(string name) => Task.FromResult(new List<User>());
        public Task<List<User>> GetAllUsersAsync() => Task.FromResult(new List<User> { user });
        public Task<User?> UpdateUserAsync(User updatedUser) => Task.FromResult<User?>(updatedUser);
        public Task<User?> UpdatePasswordAsync(Guid id, string password) => GetByIdIgnoreTenantAsync(id);
        public Task<User?> UpdatePasswordIgnoreTenantAsync(Guid id, string password) => GetByIdIgnoreTenantAsync(id);
        public Task<(List<User> Items, int TotalCount)> GetAllUserPagingAsync(int pageNumber, int pageSize)
            => Task.FromResult((new List<User> { user }, 1));
        public Task<bool?> CheckUserIsDeleted(Guid id) => Task.FromResult<bool?>(user.IsDeleted);
        public Task<List<Role>> GetRolesByUserIdAsync(Guid userId)
            => Task.FromResult(user.UserRoles.Select(userRole => userRole.Role).ToList());
        public Task<bool> AddRoleToUserAsync(Guid userId, int roleId) => Task.FromResult(true);
        public Task<bool> RemoveRoleFromUserAsync(Guid userId, int roleId) => Task.FromResult(true);
        public Task AddAsync(User newUser) => Task.CompletedTask;
        public Task<User> GetOwnerUserByIdAsync(Guid? ownerUserId) => Task.FromResult(user);
        public Task<User?> UpdateUserIgnoreTenantAsync(User updatedUser) => Task.FromResult<User?>(updatedUser);
        public Task SoftDeleteUserAndFreeEmailAsync(Guid userId) => Task.CompletedTask;
    }
}
