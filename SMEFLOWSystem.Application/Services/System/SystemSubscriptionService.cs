using ShareKernel.Common.Enum;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Logging;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services.System;

public sealed class SystemSubscriptionService : ISystemSubscriptionService
{
    private readonly ISystemSubscriptionReadRepository _repository;
    private readonly IModuleSubscriptionRepository? _commandRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<SystemSubscriptionService>? _logger;

    public SystemSubscriptionService(
        ISystemSubscriptionReadRepository repository,
        IModuleSubscriptionRepository? commandRepository = null,
        ICurrentUserService? currentUserService = null,
        ILogger<SystemSubscriptionService>? logger = null)
    {
        _repository = repository;
        _commandRepository = commandRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public Task<PagedResultDto<SystemSubscriptionDto>> GetAllAsync(
        SystemSubscriptionQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePaging(query.PageNumber, query.PageSize);
        if (query.ModuleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.ModuleId), "ModuleId must be greater than 0.");
        if (query.ExpiringFrom > query.ExpiringTo)
            throw new ArgumentException("ExpiringFrom must not be later than ExpiringTo.");

        var statuses = new[]
        {
            StatusEnum.ModuleActive,
            StatusEnum.ModuleTrial,
            StatusEnum.ModuleSuspended
        };
        if (!string.IsNullOrWhiteSpace(query.Status)
            && !statuses.Contains(query.Status.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Subscription status is not supported.", nameof(query.Status));

        return _repository.GetPagedAsync(query, DateTime.UtcNow, cancellationToken);
    }

    public async Task<SystemSubscriptionCommandResultDto> ExtendAsync(
        Guid subscriptionId,
        SystemSubscriptionExtendRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorUserId = GetActorUserId();
        var subscription = await GetMutableSubscriptionAsync(subscriptionId, cancellationToken);
        if (request.NewEndDate.Kind != DateTimeKind.Utc)
            throw new ArgumentException("NewEndDate must be a UTC timestamp.", nameof(request));
        if (request.NewEndDate <= subscription.EndDate)
            throw new InvalidOperationException("NewEndDate must be later than the current EndDate.");

        var beforeEndDate = subscription.EndDate;
        subscription.EndDate = request.NewEndDate;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _commandRepository!.SaveSystemAdminChangesAsync(subscription, cancellationToken);

        _logger?.LogWarning(
            SystemAdminLogEvents.SubscriptionExtended,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; SubscriptionId={SubscriptionId}; BeforeEndDate={BeforeEndDate}; AfterEndDate={AfterEndDate}; Reason={Reason}",
            "SUBSCRIPTION_EXTENDED",
            actorUserId,
            subscription.Id,
            beforeEndDate,
            subscription.EndDate,
            request.Reason?.Trim());
        return ToResult(subscription, true);
    }

    public async Task<SystemSubscriptionCommandResultDto> SuspendAsync(
        Guid subscriptionId,
        SystemSubscriptionReasonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorUserId = GetActorUserId();
        var subscription = await GetMutableSubscriptionAsync(subscriptionId, cancellationToken);
        if (string.Equals(subscription.Status, StatusEnum.ModuleSuspended, StringComparison.OrdinalIgnoreCase))
            return ToResult(subscription, false);
        if (!string.Equals(subscription.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(subscription.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot suspend a subscription in status {subscription.Status}.");

        var beforeStatus = subscription.Status;
        subscription.Status = StatusEnum.ModuleSuspended;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _commandRepository!.SaveSystemAdminChangesAsync(subscription, cancellationToken);

        _logger?.LogWarning(
            SystemAdminLogEvents.SubscriptionSuspended,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; SubscriptionId={SubscriptionId}; BeforeStatus={BeforeStatus}; AfterStatus={AfterStatus}; Reason={Reason}",
            "SUBSCRIPTION_SUSPENDED",
            actorUserId,
            subscription.Id,
            beforeStatus,
            subscription.Status,
            request.Reason?.Trim());
        return ToResult(subscription, true);
    }

    public async Task<SystemSubscriptionCommandResultDto> ReactivateAsync(
        Guid subscriptionId,
        SystemSubscriptionReasonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorUserId = GetActorUserId();
        var subscription = await GetMutableSubscriptionAsync(subscriptionId, cancellationToken);
        if (subscription.Module?.IsActive != true)
            throw new InvalidOperationException("The module catalog entry is inactive.");
        if (subscription.EndDate <= DateTime.UtcNow)
            throw new InvalidOperationException("An expired subscription cannot be reactivated.");
        if (!string.Equals(subscription.Status, StatusEnum.ModuleSuspended, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot reactivate a subscription in status {subscription.Status}.");

        var beforeStatus = subscription.Status;
        subscription.Status = StatusEnum.ModuleActive;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _commandRepository!.SaveSystemAdminChangesAsync(subscription, cancellationToken);

        _logger?.LogWarning(
            SystemAdminLogEvents.SubscriptionReactivated,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; SubscriptionId={SubscriptionId}; BeforeStatus={BeforeStatus}; AfterStatus={AfterStatus}; Reason={Reason}",
            "SUBSCRIPTION_REACTIVATED",
            actorUserId,
            subscription.Id,
            beforeStatus,
            subscription.Status,
            request.Reason?.Trim());
        return ToResult(subscription, true);
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private Guid GetActorUserId()
        => _currentUserService?.UserId
            ?? throw new UnauthorizedAccessException("SystemAdmin user is not resolved.");

    private async Task<ModuleSubscription> GetMutableSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var repository = _commandRepository
            ?? throw new InvalidOperationException("Subscription command repository is not configured.");
        var subscription = await repository.GetByIdIgnoreTenantAsync(subscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription not found.");
        if (subscription.IsDeleted)
            throw new InvalidOperationException("A cancelled subscription cannot be changed.");
        if (subscription.Tenant == null
            || subscription.Tenant.IsDeleted
            || string.Equals(subscription.Tenant.Name, SystemTenantConstants.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("The subscription tenant cannot be changed.");
        return subscription;
    }

    private static SystemSubscriptionCommandResultDto ToResult(
        ModuleSubscription subscription,
        bool changed)
        => new()
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            ModuleId = subscription.ModuleId,
            Status = subscription.Status,
            EndDate = subscription.EndDate,
            UpdatedAt = subscription.UpdatedAt,
            IsDeleted = subscription.IsDeleted,
            Changed = changed
        };
}
