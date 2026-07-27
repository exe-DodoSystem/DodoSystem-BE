using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

public sealed class ModuleSubscriptionAuthorizationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(RoleConstants.Employee)]
    [InlineData(RoleConstants.Manager)]
    [InlineData(RoleConstants.HrManager)]
    [InlineData(RoleConstants.SystemAdmin)]
    public async Task Cancel_NonTenantAdmin_IsRejectedBeforeRepositoryAccess(
        string? role)
    {
        var tenantId = Guid.NewGuid();
        var subscription = Subscription(tenantId, moduleId: 7);
        var subscriptions = new ModuleSubscriptionRepositoryStub(subscription);
        var service = CreateService(
            tenantId,
            role,
            subscriptions,
            new BillingOrderRepositoryStub());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CancelMyModuleSubscriptionAsync(7));

        Assert.Equal(0, subscriptions.GetByTenantAndModuleCalls);
        Assert.Equal(0, subscriptions.UpdateCalls);
        Assert.False(subscription.IsDeleted);
        Assert.Equal("Active", subscription.Status);
    }

    [Fact]
    public async Task Cancel_TenantAdmin_OnlyMutatesCurrentTenantSubscription()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subscriptionA = Subscription(tenantA, moduleId: 7);
        var subscriptionB = Subscription(tenantB, moduleId: 7);
        var subscriptions = new ModuleSubscriptionRepositoryStub(
            subscriptionA,
            subscriptionB);
        var service = CreateService(
            tenantA,
            RoleConstants.TenantAdmin,
            subscriptions,
            new BillingOrderRepositoryStub());

        var result = await service.CancelMyModuleSubscriptionAsync(7);

        Assert.True(result);
        Assert.True(subscriptionA.IsDeleted);
        Assert.Equal("Suspended", subscriptionA.Status);
        Assert.False(subscriptionB.IsDeleted);
        Assert.Equal("Active", subscriptionB.Status);
        Assert.Equal((tenantA, 7), subscriptions.LastLookup);
        Assert.Equal(1, subscriptions.UpdateCalls);
    }

    [Fact]
    public async Task Cancel_TenantAdmin_MissingSubscriptionReturnsTypedNotFound()
    {
        var tenantId = Guid.NewGuid();
        var subscriptions = new ModuleSubscriptionRepositoryStub();
        var service = CreateService(
            tenantId,
            RoleConstants.TenantAdmin,
            subscriptions,
            new BillingOrderRepositoryStub());

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CancelMyModuleSubscriptionAsync(99));

        Assert.Contains("Không tìm thấy module", exception.Message);
        Assert.Equal(1, subscriptions.GetByTenantAndModuleCalls);
        Assert.Equal(0, subscriptions.UpdateCalls);
    }

    private static ModuleSubscriptionService CreateService(
        Guid? tenantId,
        string? role,
        IModuleSubscriptionRepository subscriptions,
        IBillingOrderRepository billingOrders)
    {
        return new ModuleSubscriptionService(
            mapper: null!,
            currentTenantService: new CurrentTenantServiceStub(tenantId),
            moduleRepo: null!,
            moduleSubscriptionRepo: subscriptions,
            billingOrderRepo: billingOrders,
            billingOrderModuleRepo: null!,
            currentUser: new CurrentUserServiceStub(role));
    }

    private static ModuleSubscription Subscription(Guid tenantId, int moduleId)
    {
        return new ModuleSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = "Active",
            IsDeleted = false
        };
    }

    private sealed class CurrentUserServiceStub : ICurrentUserService
    {
        private readonly string? _role;

        public CurrentUserServiceStub(string? role)
        {
            _role = role;
        }

        public Guid? UserId { get; } = Guid.NewGuid();

        public bool IsInRole(string role)
        {
            return string.Equals(_role, role, StringComparison.Ordinal);
        }
    }

    private sealed class CurrentTenantServiceStub : ICurrentTenantService
    {
        public CurrentTenantServiceStub(Guid? tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }

    private sealed class ModuleSubscriptionRepositoryStub
        : IModuleSubscriptionRepository
    {
        private readonly Dictionary<(Guid TenantId, int ModuleId), ModuleSubscription>
            _subscriptions;

        public ModuleSubscriptionRepositoryStub(
            params ModuleSubscription[] subscriptions)
        {
            _subscriptions = subscriptions.ToDictionary(
                subscription => (subscription.TenantId, subscription.ModuleId));
        }

        public int GetByTenantAndModuleCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public (Guid TenantId, int ModuleId)? LastLookup { get; private set; }

        public Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(
            Guid tenantId,
            int moduleId)
        {
            GetByTenantAndModuleCalls++;
            LastLookup = (tenantId, moduleId);
            _subscriptions.TryGetValue((tenantId, moduleId), out var subscription);
            return Task.FromResult(subscription);
        }

        public Task UpdateIgnoreTenantAsync(ModuleSubscription subscription)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task AddAsync(ModuleSubscription subscription)
            => throw new NotSupportedException();

        public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(
            Guid tenantId)
            => throw new NotSupportedException();

        public Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId)
            => throw new NotSupportedException();

        public Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync()
            => throw new NotSupportedException();

        public Task<ModuleSubscription?> GetByIdIgnoreTenantAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveSystemAdminChangesAsync(
            ModuleSubscription subscription,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class BillingOrderRepositoryStub : IBillingOrderRepository
    {
        public Task<List<BillingOrder>> GetByTenantIdAsync(Guid tenantId)
        {
            return Task.FromResult(new List<BillingOrder>());
        }

        public Task<BillingOrder?> GetByIdAsync(Guid billingOrderId)
            => throw new NotSupportedException();

        public Task<BillingOrder?> GetByIdIgnoreTenantAsync(Guid billingOrderId)
            => throw new NotSupportedException();

        public Task<BillingOrder?> GetByOrderNumberIgnoreTenantAsync(
            string billingOrderNumber)
            => throw new NotSupportedException();

        public Task AddAsync(BillingOrder billingOrder)
            => throw new NotSupportedException();

        public Task<BillingOrder?> UpdateAsync(BillingOrder billingOrder)
            => throw new NotSupportedException();

        public Task<BillingOrder?> UpdateIgnoreTenantAsync(BillingOrder billingOrder)
            => throw new NotSupportedException();
    }
}
