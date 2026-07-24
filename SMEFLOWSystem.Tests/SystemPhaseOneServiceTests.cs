using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemPhaseOneServiceTests
{
    [Fact]
    public async Task ModuleTrend_Over24Months_IsRejected()
    {
        var service = new SystemDashboardService(new DashboardRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetModuleTrendsAsync(
            new SystemModuleTrendQueryDto
            {
                FromMonth = 1,
                FromYear = 2024,
                ToMonth = 1,
                ToYear = 2026
            }));
    }

    [Fact]
    public async Task Subscription_InvertedExpirationRange_IsRejected()
    {
        var service = new SystemSubscriptionService(new SubscriptionRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllAsync(
            new SystemSubscriptionQueryDto
            {
                ExpiringFrom = new DateTime(2026, 8, 1),
                ExpiringTo = new DateTime(2026, 7, 1)
            }));
    }

    [Fact]
    public async Task Billing_UnsupportedSort_IsRejected()
    {
        var service = new SystemBillingService(new BillingRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetBillingOrdersAsync(
            new SystemBillingOrderQueryDto { SortBy = "tenant.passwordHash" }));
    }

    [Fact]
    public async Task Payment_PageSizeOver100_IsRejected()
    {
        var service = new SystemBillingService(new BillingRepositoryStub());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetPaymentTransactionsAsync(
                new SystemPaymentTransactionQueryDto { PageSize = 101 }));
    }

    [Theory]
    [InlineData(typeof(SystemTenantUserDto))]
    [InlineData(typeof(SystemPaymentTransactionDto))]
    public void PublicSystemDtos_DoNotExposeSecrets(Type dtoType)
    {
        var propertyNames = dtoType.GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("PasswordHash", propertyNames);
        Assert.DoesNotContain("TokenHash", propertyNames);
        Assert.DoesNotContain("RawData", propertyNames);
    }

    private sealed class SubscriptionRepositoryStub : ISystemSubscriptionReadRepository
    {
        public Task<PagedResultDto<SystemSubscriptionDto>> GetPagedAsync(
            SystemSubscriptionQueryDto query,
            DateTime nowUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemSubscriptionDto>());
    }

    private sealed class BillingRepositoryStub : ISystemBillingReadRepository
    {
        public Task<PagedResultDto<SystemBillingOrderListItemDto>> GetBillingOrdersAsync(
            SystemBillingOrderQueryDto query,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemBillingOrderListItemDto>());

        public Task<SystemBillingOrderDetailDto?> GetBillingOrderAsync(
            Guid billingOrderId,
            CancellationToken cancellationToken)
            => Task.FromResult<SystemBillingOrderDetailDto?>(null);

        public Task<PagedResultDto<SystemPaymentTransactionDto>> GetPaymentTransactionsAsync(
            SystemPaymentTransactionQueryDto query,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemPaymentTransactionDto>());
    }

    private sealed class DashboardRepositoryStub : ISystemDashboardReadRepository
    {
        public Task<SystemDashboardOverviewDto> GetOverviewAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            DateOnly today,
            CancellationToken cancellationToken)
            => Task.FromResult(new SystemDashboardOverviewDto());

        public Task<List<ModuleUsageStatDto>> GetModuleUsageAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ModuleUsageStatDto>());

        public Task<List<ModuleCancellationStatDto>> GetModuleCancellationsAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ModuleCancellationStatDto>());

        public Task<List<ModuleExpirationStatDto>> GetModuleExpirationsAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ModuleExpirationStatDto>());

        public Task<SystemModuleTrendDto> GetModuleTrendsAsync(
            DateTime fromUtc,
            DateTime toExclusiveUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new SystemModuleTrendDto());
    }
}
