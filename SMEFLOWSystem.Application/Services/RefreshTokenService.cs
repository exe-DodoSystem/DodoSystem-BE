using Microsoft.Extensions.Configuration;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.RefreshTokenDtos;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System.Security.Cryptography;
using System.Text;

namespace SMEFLOWSystem.Application.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private const int DefaultRefreshTokenExpiryDays = 7;
    private const string RotatedReason = "Rotated";
    private const string ReuseDetectedReason = "Refresh token reuse detected";

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IModuleSubscriptionRepository _moduleSubscriptionRepository;
    private readonly IConfiguration _config;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IModuleSubscriptionRepository moduleSubscriptionRepository,
        IConfiguration config)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _moduleSubscriptionRepository = moduleSubscriptionRepository;
        _config = config;
    }

    public async Task<RefreshTokenResponseDto> IssueAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdIgnoreTenantAsync(userId);
        if (user == null)
            throw new ArgumentException("Không tìm thấy user");
        if (!CanIssueTokens(user))
            throw new ArgumentException("Tài khoản hoặc công ty không còn khả dụng");

        var now = DateTime.UtcNow;
        var isModulesExpired = await ComputeIsModulesExpiredAsync(user, now);
        var accessToken = AuthHelper.GenerateJwtToken(user, _config, isModulesExpired);
        var (rawToken, tokenEntity) = CreateRefreshToken(user, now);

        await _refreshTokenRepository.AddAsync(tokenEntity);

        return new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawToken,
            IsExpired = isModulesExpired
        };
    }

    public async Task<(bool success, RefreshTokenResponseDto? response, string message)> RefreshAsync(
        RefreshRequestDto request)
    {
        var rawToken = (request?.RefreshToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
            return (false, null, "RefreshToken là bắt buộc");

        var tokenHash = HashToken(rawToken);
        var existingToken = await _refreshTokenRepository
            .GetByTokenHashIgnoreTenantAsync(tokenHash);
        if (existingToken == null)
            return (false, null, "RefreshToken không hợp lệ");

        if (existingToken.RevokedAt != null)
        {
            if (existingToken.ReplacedByTokenId.HasValue)
            {
                await RevokeCompromisedSessionAsync(existingToken);
            }

            return (false, null, "RefreshToken đã bị thu hồi");
        }

        var now = DateTime.UtcNow;
        if (existingToken.ExpiresAt <= now)
            return (false, null, "RefreshToken đã hết hạn");

        var user = await _userRepository.GetByIdIgnoreTenantAsync(existingToken.UserId);
        if (user == null || !CanIssueTokens(user))
        {
            await _refreshTokenRepository.RevokeAllAsync(
                existingToken.UserId,
                existingToken.TenantId,
                "Account or tenant unavailable");
            return (false, null, "Tài khoản hoặc công ty không còn khả dụng");
        }

        var isModulesExpired = await ComputeIsModulesExpiredAsync(user, now);
        var accessToken = AuthHelper.GenerateJwtToken(user, _config, isModulesExpired);
        var (newRawToken, replacementToken) = CreateRefreshToken(user, now);
        replacementToken.TenantId = existingToken.TenantId;

        var rotated = await _refreshTokenRepository.RotateAsync(
            existingToken.Id,
            replacementToken,
            now,
            RotatedReason);
        if (!rotated)
        {
            await RevokeCompromisedSessionAsync(existingToken);
            return (false, null, "RefreshToken đã được sử dụng");
        }

        return (true, new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawToken,
            IsExpired = isModulesExpired
        }, string.Empty);
    }

    public async Task RevokeAllAsync(Guid userId, string reason)
    {
        var user = await _userRepository.GetByIdIgnoreTenantAsync(userId);
        if (user == null)
            throw new ArgumentException("Không tìm thấy user");

        await _refreshTokenRepository.RevokeAllAsync(
            user.Id,
            user.TenantId,
            string.IsNullOrWhiteSpace(reason) ? "Revoked" : reason);
    }

    public async Task<List<RefreshTokenDto>> GetAllByUserIdAsync(Guid userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
            throw new ArgumentException("Không tìm thấy user");

        var tokens = await _refreshTokenRepository.GetByUserIdAsync(user.Id, user.TenantId);
        return tokens.Select(token => new RefreshTokenDto
        {
            Id = token.Id,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            RevokedAt = token.RevokedAt
        }).ToList();
    }

    private async Task<bool> ComputeIsModulesExpiredAsync(User user, DateTime nowUtc)
    {
        var isSystemAdmin = user.UserRoles?.Any(userRole => userRole.Role != null
            && string.Equals(
                userRole.Role.Name,
                "SystemAdmin",
                StringComparison.OrdinalIgnoreCase)) == true;
        if (isSystemAdmin)
            return false;

        var subscriptions = await _moduleSubscriptionRepository
            .GetByTenantIgnoreTenantAsync(user.TenantId);
        return !subscriptions.Any(subscription =>
            ModuleSubscriptionRules.IsUsable(subscription, nowUtc));
    }

    private static bool CanIssueTokens(User user)
    {
        if (!user.IsActive || user.IsDeleted || user.Tenant == null || user.Tenant.IsDeleted)
            return false;

        return string.Equals(user.Tenant.Status, StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Tenant.Status, StatusEnum.TenantTrial, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Tenant.Status, StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase);
    }

    private Task RevokeCompromisedSessionAsync(RefreshToken token)
        => _refreshTokenRepository.RevokeAllAsync(
            token.UserId,
            token.TenantId,
            ReuseDetectedReason);

    private (string rawToken, RefreshToken entity) CreateRefreshToken(
        User user,
        DateTime nowUtc)
    {
        var rawToken = GenerateSecureToken();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.AddDays(GetRefreshTokenExpiryDays())
        };

        return (rawToken, entity);
    }

    private int GetRefreshTokenExpiryDays()
    {
        var raw = _config["Jwt:RefreshTokenExpiryDays"]
            ?? _config["Jwt:RefreshTokenDays"];
        return int.TryParse(raw, out var days) && days > 0
            ? days
            : DefaultRefreshTokenExpiryDays;
    }

    private static string GenerateSecureToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
}
