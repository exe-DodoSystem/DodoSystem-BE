using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using ShareKernel.Common.Enum;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services.System;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

public sealed class SystemPhaseTwoServiceTests
{
    [Theory]
    [InlineData(StatusEnum.ModuleSuspended, false, -1, 1, false)]
    [InlineData(StatusEnum.ModuleActive, true, -1, 1, false)]
    [InlineData(StatusEnum.ModuleActive, false, 1, 2, false)]
    [InlineData(StatusEnum.ModuleActive, false, -2, -1, false)]
    [InlineData(StatusEnum.ModuleActive, false, 0, 1, true)]
    [InlineData(StatusEnum.ModuleActive, false, -1, 0, false)]
    [InlineData(StatusEnum.ModuleActive, false, -1, 1, true)]
    [InlineData(StatusEnum.ModuleTrial, false, -1, 1, true)]
    public void ModuleSubscriptionRule_EnforcesAllUsabilityConditions(
        string status,
        bool isDeleted,
        int startOffsetHours,
        int endOffsetHours,
        bool expected)
    {
        var now = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var subscription = new ModuleSubscription
        {
            Status = status,
            IsDeleted = isDeleted,
            StartDate = now.AddHours(startOffsetHours),
            EndDate = now.AddHours(endOffsetHours)
        };

        Assert.Equal(expected, ModuleSubscriptionRules.IsUsable(subscription, now));
    }

    [Fact]
    public async Task TenantSystem_CannotBeSuspended()
    {
        var tenant = TenantEntity(StatusEnum.TenantActive, SystemTenantConstants.Name);
        var repository = new TenantRepositoryStub(tenant);
        var service = CreateTenantService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeStatusAsync(
            tenant.Id,
            new SystemTenantStatusUpdateDto
            {
                Status = StatusEnum.TenantSuspended,
                Reason = "test"
            }));
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task TenantPendingPayment_CannotBeActivated()
    {
        var tenant = TenantEntity(StatusEnum.TenantPending);
        var repository = new TenantRepositoryStub(tenant);
        var service = CreateTenantService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeStatusAsync(
            tenant.Id,
            new SystemTenantStatusUpdateDto { Status = StatusEnum.TenantActive }));
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task TenantSameStatus_IsIdempotent()
    {
        var tenant = TenantEntity(StatusEnum.TenantActive);
        var repository = new TenantRepositoryStub(tenant);
        var service = CreateTenantService(repository);

        var result = await service.ChangeStatusAsync(
            tenant.Id,
            new SystemTenantStatusUpdateDto { Status = StatusEnum.TenantActive });

        Assert.False(result.Changed);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task TenantSuspend_UpdatesOnlyStatus()
    {
        var tenant = TenantEntity(StatusEnum.TenantTrial);
        var originalName = tenant.Name;
        var repository = new TenantRepositoryStub(tenant);
        var logger = new CaptureLogger<SystemTenantService>();
        var service = CreateTenantService(repository, logger);

        var result = await service.ChangeStatusAsync(
            tenant.Id,
            new SystemTenantStatusUpdateDto
            {
                Status = StatusEnum.TenantSuspended,
                Reason = "manual review"
            });

        Assert.True(result.Changed);
        Assert.Equal(StatusEnum.TenantSuspended, tenant.Status);
        Assert.Equal(originalName, tenant.Name);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(5101, logger.LastEventId?.Id);
    }

    [Fact]
    public async Task SubscriptionExtend_DoesNotChangeStatus()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        var originalStatus = subscription.Status;
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);
        var newEndDate = subscription.EndDate.AddMonths(1);

        var result = await service.ExtendAsync(
            subscription.Id,
            new SystemSubscriptionExtendRequestDto { NewEndDate = newEndDate });

        Assert.True(result.Changed);
        Assert.Equal(newEndDate, subscription.EndDate);
        Assert.Equal(originalStatus, subscription.Status);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task SubscriptionExtend_CannotShortenEndDate()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleActive);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtendAsync(
            subscription.Id,
            new SystemSubscriptionExtendRequestDto
            {
                NewEndDate = subscription.EndDate.AddMinutes(-1)
            }));
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task SubscriptionSuspend_DoesNotSoftDelete()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleActive);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        var result = await service.SuspendAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto { Reason = "manual review" });

        Assert.Equal(StatusEnum.ModuleSuspended, result.Status);
        Assert.False(subscription.IsDeleted);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task SubscriptionSuspendRepeated_IsIdempotent()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        var result = await service.SuspendAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto());

        Assert.False(result.Changed);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task ExpiredSubscription_CannotBeReactivated()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        subscription.EndDate = DateTime.UtcNow.AddMinutes(-1);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReactivateAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto()));
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task CancelledSubscription_CannotBeReactivated()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        subscription.IsDeleted = true;
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReactivateAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto()));
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task ActiveSubscription_CannotBeReactivatedAgain()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleActive);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReactivateAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto()));
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task InactiveCatalogModule_CannotBeReactivated()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        subscription.Module!.IsActive = false;
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReactivateAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto()));
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task SuspendedSubscription_CanBeReactivated()
    {
        var subscription = SubscriptionEntity(StatusEnum.ModuleSuspended);
        var repository = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateSubscriptionService(repository);

        var result = await service.ReactivateAsync(
            subscription.Id,
            new SystemSubscriptionReasonRequestDto { Reason = "contract restored" });

        Assert.True(result.Changed);
        Assert.Equal(StatusEnum.ModuleActive, subscription.Status);
        Assert.Equal(1, repository.SaveCalls);
    }

    private static SystemTenantService CreateTenantService(
        ITenantRepository repository,
        ILogger<SystemTenantService>? logger = null)
        => new(
            new TenantReadRepositoryStub(),
            repository,
            new CurrentUserStub(),
            logger ?? NullLogger<SystemTenantService>.Instance);

    private static SystemSubscriptionService CreateSubscriptionService(
        IModuleSubscriptionRepository repository)
        => new(
            new SubscriptionReadRepositoryStub(),
            repository,
            new CurrentUserStub(),
            NullLogger<SystemSubscriptionService>.Instance);

    private static Tenant TenantEntity(string status, string name = "Customer")
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = status,
            IsDeleted = false
        };

    private static ModuleSubscription SubscriptionEntity(string status)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ModuleId = 1,
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            IsDeleted = false,
            Tenant = TenantEntity(StatusEnum.TenantActive),
            Module = new Module { Id = 1, IsActive = true }
        };

    private sealed class CurrentUserStub : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsInRole(string role) => role == RoleConstants.SystemAdmin;
    }

    private sealed class TenantRepositoryStub : ITenantRepository
    {
        private readonly Tenant _tenant;
        public TenantRepositoryStub(Tenant tenant) => _tenant = tenant;
        public int UpdateCalls { get; private set; }

        public Task AddAsync(Tenant tenant) => Task.CompletedTask;
        public Task<List<Tenant>> GetAllIgnoreTenantAsync() => Task.FromResult(new List<Tenant> { _tenant });
        public Task<Tenant?> GetByIdAsync(Guid tenantId) => Task.FromResult<Tenant?>(_tenant);
        public Task<Tenant?> GetByIdIgnoreTenantAsync(Guid tenantId)
            => Task.FromResult<Tenant?>(tenantId == _tenant.Id ? _tenant : null);
        public Task<Tenant?> GetByOwnerUserIdIgnoreAsync(Guid ownerId) => Task.FromResult<Tenant?>(null);
        public Task<List<Tenant>> GetExpiredTenantsIgnoreTenantAsync(DateOnly todayUtc)
            => Task.FromResult(new List<Tenant>());
        public Task<(List<Tenant> Items, int TotalCount)> GetPagedIgnoreTenantAsync(int pageNumber, int pageSize)
            => Task.FromResult((new List<Tenant> { _tenant }, 1));
        public Task UpdateAsync(Tenant tenant) => Task.CompletedTask;
        public Task UpdateIgnoreTenantAsync(Tenant tenant) => Task.CompletedTask;
        public Task UpdateStatusIgnoreTenantAsync(
            Guid tenantId,
            string status,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            UpdateCalls++;
            _tenant.Status = status;
            _tenant.UpdatedAt = updatedAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class ModuleSubscriptionRepositoryStub : IModuleSubscriptionRepository
    {
        private readonly ModuleSubscription _subscription;
        public ModuleSubscriptionRepositoryStub(ModuleSubscription subscription)
            => _subscription = subscription;
        public int SaveCalls { get; private set; }

        public Task AddAsync(ModuleSubscription subscription) => Task.CompletedTask;
        public Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync()
            => Task.FromResult(new List<ModuleSubscription> { _subscription });
        public Task<ModuleSubscription?> GetByIdIgnoreTenantAsync(Guid subscriptionId, CancellationToken cancellationToken)
            => Task.FromResult<ModuleSubscription?>(subscriptionId == _subscription.Id ? _subscription : null);
        public Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(Guid tenantId, int moduleId)
            => Task.FromResult<ModuleSubscription?>(_subscription);
        public Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId)
            => Task.FromResult(new List<ModuleSubscription> { _subscription });
        public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId)
            => Task.FromResult(new List<ModuleSubscription> { _subscription });
        public Task SaveSystemAdminChangesAsync(ModuleSubscription subscription, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
        public Task UpdateIgnoreTenantAsync(ModuleSubscription subscription) => Task.CompletedTask;
    }

    private sealed class TenantReadRepositoryStub : ISystemTenantReadRepository
    {
        public Task<SystemTenantDetailDto?> GetByIdAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken)
            => Task.FromResult<SystemTenantDetailDto?>(null);
        public Task<PagedResultDto<SystemTenantListItemDto>> GetPagedAsync(
            SystemTenantQueryDto query,
            DateOnly today,
            DateTime nowUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemTenantListItemDto>());
        public Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
            Guid tenantId,
            SystemTenantUserQueryDto query,
            CancellationToken cancellationToken)
            => Task.FromResult<PagedResultDto<SystemTenantUserDto>?>(null);
    }

    private sealed class SubscriptionReadRepositoryStub : ISystemSubscriptionReadRepository
    {
        public Task<PagedResultDto<SystemSubscriptionDto>> GetPagedAsync(
            SystemSubscriptionQueryDto query,
            DateTime nowUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemSubscriptionDto>());
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public EventId? LastEventId { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastEventId = eventId;
        }
    }
}
