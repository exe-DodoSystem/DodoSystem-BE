using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Tests;

public sealed class BillingOrderServiceTests
{
    [Theory]
    [InlineData(typeof(BillingOrder))]
    [InlineData(typeof(BillingOrderDto))]
    [InlineData(typeof(SystemBillingOrderListItemDto))]
    [InlineData(typeof(SystemBillingOrderDetailDto))]
    public void BillingOrderContracts_ExposeOnlyTotalAmount(Type contractType)
    {
        Assert.NotNull(contractType.GetProperty(nameof(BillingOrder.TotalAmount)));
        Assert.Null(contractType.GetProperty("DiscountAmount"));
        Assert.Null(contractType.GetProperty("FinalAmount"));
    }

    [Fact]
    public void SalesOrder_KeepsItsIndependentDiscountContract()
    {
        Assert.NotNull(typeof(Order).GetProperty(nameof(Order.DiscountAmount)));
        Assert.NotNull(typeof(Order).GetProperty(nameof(Order.FinalAmount)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CreateModuleBillingOrder_SumsEveryModuleWithoutQuantityDiscount(
        int moduleCount)
    {
        var modules = Enumerable.Range(1, moduleCount)
            .Select(index => new Module
            {
                Id = index,
                Code = $"MODULE_{index}",
                ShortCode = $"M{index}",
                Name = $"Module {index}",
                MonthlyPrice = index * 100_000m,
                IsActive = true
            })
            .ToList();
        var orderRepository = new FakeBillingOrderRepository();
        var lineRepository = new FakeBillingOrderModuleRepository();
        var service = new BillingOrderService(
            orderRepository,
            new FakeModuleRepository(modules),
            lineRepository,
            new FakeModuleSubscriptionRepository(),
            null!);

        var order = await service.CreateModuleBillingOrderAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            modules.Select(module => module.Id).ToArray());

        var expectedTotal = modules.Sum(module => module.MonthlyPrice);
        Assert.Equal(expectedTotal, order.TotalAmount);
        Assert.Equal(expectedTotal, lineRepository.Added.Sum(line => line.LineTotal));
        Assert.Equal(moduleCount, lineRepository.Added.Count);
        Assert.All(lineRepository.Added, line => Assert.Equal(1, line.Quantity));
        Assert.Same(order, orderRepository.Added);
    }

    [Fact]
    public async Task CreateModuleBillingOrder_KeepsProrationWithoutApplyingDiscount()
    {
        var modules = new List<Module>
        {
            new()
            {
                Id = 1,
                Code = "MODULE_1",
                ShortCode = "M1",
                Name = "Module 1",
                MonthlyPrice = 300_000m,
                IsActive = true
            },
            new()
            {
                Id = 2,
                Code = "MODULE_2",
                ShortCode = "M2",
                Name = "Module 2",
                MonthlyPrice = 600_000m,
                IsActive = true
            }
        };
        var lineRepository = new FakeBillingOrderModuleRepository();
        var service = new BillingOrderService(
            new FakeBillingOrderRepository(),
            new FakeModuleRepository(modules),
            lineRepository,
            new FakeModuleSubscriptionRepository(),
            null!);
        var prorateUntil = DateTime.UtcNow.Date.AddDays(10);

        var order = await service.CreateModuleBillingOrderAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            modules.Select(module => module.Id).ToArray(),
            prorateUntilUtc: prorateUntil);

        var expectedTotal = modules.Sum(module =>
            decimal.Floor((module.MonthlyPrice / 30m) * 10m));
        Assert.Equal(expectedTotal, order.TotalAmount);
        Assert.Equal(expectedTotal, lineRepository.Added.Sum(line => line.LineTotal));
    }

    private sealed class FakeModuleRepository(IReadOnlyCollection<Module> modules)
        : IModuleRepository
    {
        public Task<List<Module>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var requested = ids.ToHashSet();
            return Task.FromResult(modules
                .Where(module => requested.Contains(module.Id))
                .ToList());
        }

        public Task<List<Module>> GetAllActiveAsync()
            => Task.FromResult(modules.Where(module => module.IsActive).ToList());

        public Task<List<Module>> GetAllAsync()
            => Task.FromResult(modules.ToList());

        public Task AddAsync(Module module) => Task.CompletedTask;

        public Task<bool> ExistsByCodeOrShortCodeAsync(string code, string shortCode)
            => Task.FromResult(modules.Any(module =>
                module.Code == code || module.ShortCode == shortCode));

        public Task<Module?> GetByCodeAsync(string code)
            => Task.FromResult(modules.FirstOrDefault(module => module.Code == code));

        public Task<Module?> GetByIdAsync(int id)
            => Task.FromResult(modules.FirstOrDefault(module => module.Id == id));

        public Task<Module> UpdateAsync(Module module) => Task.FromResult(module);
    }

    private sealed class FakeBillingOrderRepository : IBillingOrderRepository
    {
        public BillingOrder? Added { get; private set; }

        public Task AddAsync(BillingOrder billingOrder)
        {
            Added = billingOrder;
            return Task.CompletedTask;
        }

        public Task<BillingOrder?> GetByIdAsync(Guid billingOrderId)
            => Task.FromResult<BillingOrder?>(null);

        public Task<List<BillingOrder>> GetByTenantIdAsync(Guid tenantId)
            => Task.FromResult(new List<BillingOrder>());

        public Task<BillingOrder?> GetByIdIgnoreTenantAsync(Guid billingOrderId)
            => Task.FromResult<BillingOrder?>(null);

        public Task<BillingOrder?> GetByOrderNumberIgnoreTenantAsync(
            string billingOrderNumber)
            => Task.FromResult<BillingOrder?>(null);

        public Task<BillingOrder?> UpdateAsync(BillingOrder billingOrder)
            => Task.FromResult<BillingOrder?>(billingOrder);

        public Task<BillingOrder?> UpdateIgnoreTenantAsync(BillingOrder billingOrder)
            => Task.FromResult<BillingOrder?>(billingOrder);
    }

    private sealed class FakeBillingOrderModuleRepository
        : IBillingOrderModuleRepository
    {
        public List<BillingOrderModule> Added { get; } = [];

        public Task AddRangeAsync(IEnumerable<BillingOrderModule> items)
        {
            Added.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<List<BillingOrderModule>> GetByBillingOrderIdIgnoreTenantAsync(
            Guid billingOrderId)
            => Task.FromResult(new List<BillingOrderModule>());

        public Task<List<BillingOrderModule>> GetByTenantAndModuleAsync(
            Guid tenantId,
            int moduleId)
            => Task.FromResult(new List<BillingOrderModule>());

        public Task<List<BillingOrderModule>> GetByBillingOrderId(Guid billingOrderId)
            => Task.FromResult(new List<BillingOrderModule>());
    }

    private sealed class FakeModuleSubscriptionRepository
        : IModuleSubscriptionRepository
    {
        public Task<ModuleSubscription?> GetByTenantAndModuleIgnoreTenantAsync(
            Guid tenantId,
            int moduleId)
            => Task.FromResult<ModuleSubscription?>(null);

        public Task AddAsync(ModuleSubscription subscription) => Task.CompletedTask;

        public Task UpdateIgnoreTenantAsync(ModuleSubscription subscription)
            => Task.CompletedTask;

        public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId)
            => Task.FromResult(new List<ModuleSubscription>());

        public Task<List<ModuleSubscription>> GetByTenantIdAsync(Guid tenantId)
            => Task.FromResult(new List<ModuleSubscription>());

        public Task<List<ModuleSubscription>> GetAllIgnoreTenantAsync()
            => Task.FromResult(new List<ModuleSubscription>());

        public Task<ModuleSubscription?> GetByIdIgnoreTenantAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken)
            => Task.FromResult<ModuleSubscription?>(null);

        public Task SaveSystemAdminChangesAsync(
            ModuleSubscription subscription,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
