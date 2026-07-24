using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using Microsoft.Extensions.Logging;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Logging;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services.System;

public class SystemTenantService : ISystemTenantService
{
    private readonly ISystemTenantReadRepository _readRepository;
    private readonly ITenantRepository? _tenantRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<SystemTenantService>? _logger;

    public SystemTenantService(
        ISystemTenantReadRepository readRepository,
        ITenantRepository? tenantRepository = null,
        ICurrentUserService? currentUserService = null,
        ILogger<SystemTenantService>? logger = null)
    {
        _readRepository = readRepository;
        _tenantRepository = tenantRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public Task<PagedResultDto<SystemTenantListItemDto>> GetAllAsync(
        SystemTenantQueryDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateQuery(request);
        return _readRepository.GetPagedAsync(
            request,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow,
            cancellationToken);
    }

    public Task<SystemTenantDetailDto?> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return _readRepository.GetByIdAsync(
            tenantId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);
    }

    public Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
        Guid tenantId,
        SystemTenantUserQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePaging(query.PageNumber, query.PageSize);
        return _readRepository.GetUsersAsync(tenantId, query, cancellationToken);
    }

    public async Task<SystemTenantStatusResultDto> ChangeStatusAsync(
        Guid tenantId,
        SystemTenantStatusUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repository = _tenantRepository
            ?? throw new InvalidOperationException("Tenant command repository is not configured.");
        var actorUserId = _currentUserService?.UserId
            ?? throw new UnauthorizedAccessException("SystemAdmin user is not resolved.");

        var requestedStatus = NormalizeTenantCommandStatus(request.Status);
        var reason = request.Reason?.Trim();
        if (requestedStatus == StatusEnum.TenantSuspended && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required when suspending a tenant.", nameof(request));

        var tenant = await repository.GetByIdIgnoreTenantAsync(tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");
        if (tenant.IsDeleted)
            throw new KeyNotFoundException("Tenant not found.");
        if (string.Equals(tenant.Name, SystemTenantConstants.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("The SYSTEM tenant cannot be changed.");

        if (string.Equals(tenant.Status, requestedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return new SystemTenantStatusResultDto
            {
                TenantId = tenant.Id,
                Status = tenant.Status,
                UpdatedAt = tenant.UpdatedAt,
                Changed = false
            };
        }

        var canTransition = requestedStatus == StatusEnum.TenantSuspended
            ? string.Equals(tenant.Status, StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tenant.Status, StatusEnum.TenantTrial, StringComparison.OrdinalIgnoreCase)
            : string.Equals(tenant.Status, StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase);
        if (!canTransition)
        {
            throw new InvalidOperationException(
                $"Cannot change tenant status from {tenant.Status} to {requestedStatus}.");
        }

        var beforeStatus = tenant.Status;
        var nowUtc = DateTime.UtcNow;
        await repository.UpdateStatusIgnoreTenantAsync(
            tenant.Id,
            requestedStatus,
            nowUtc,
            cancellationToken);

        _logger?.LogWarning(
            SystemAdminLogEvents.TenantStatusChanged,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; TenantId={TenantId}; BeforeStatus={BeforeStatus}; AfterStatus={AfterStatus}; Reason={Reason}",
            "TENANT_STATUS_CHANGED",
            actorUserId,
            tenant.Id,
            beforeStatus,
            requestedStatus,
            reason);

        return new SystemTenantStatusResultDto
        {
            TenantId = tenant.Id,
            Status = requestedStatus,
            UpdatedAt = nowUtc,
            Changed = true
        };
    }

    private static void ValidateQuery(SystemTenantQueryDto request)
    {
        ValidatePaging(request.PageNumber, request.PageSize);
        if (request.ExpiringInDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(request.ExpiringInDays), "ExpiringInDays must be between 1 and 365.");
        if (request.ModuleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.ModuleId), "ModuleId must be greater than 0.");

        var allowedSortFields = new[] { "name", "status", "createdAt", "subscriptionEndDate" };
        if (!allowedSortFields.Contains(request.SortBy, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("SortBy is not supported.", nameof(request.SortBy));

        if (!string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SortDirection must be 'asc' or 'desc'.", nameof(request.SortDirection));

        var allowedStatuses = new[] { "Active", "Trial", "PendingPayment", "Suspended" };
        if (!string.IsNullOrWhiteSpace(request.Status)
            && !allowedStatuses.Contains(request.Status.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Status is not supported.", nameof(request.Status));
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "PageNumber must be at least 1.");
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be between 1 and 100.");
    }

    private static string NormalizeTenantCommandStatus(string status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "active" => StatusEnum.TenantActive,
            "suspended" => StatusEnum.TenantSuspended,
            _ => throw new ArgumentException("Status must be Active or Suspended.", nameof(status))
        };
}
