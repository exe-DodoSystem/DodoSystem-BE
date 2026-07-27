using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services.System;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

public sealed class SystemAnalyticsReadRepositoryTests
{
    [Fact]
    public async Task RevenueQueries_ExcludeSystemDeletedCancelledAndOutOfRangeRows()
    {
        await using var context = CreateContext();
        var validTenant = Tenant("Valid tenant");
        var systemTenant = Tenant("SYSTEM");
        var deletedTenant = Tenant("Deleted tenant", isDeleted: true);
        context.Tenants.AddRange(validTenant, systemTenant, deletedTenant);

        var inRange = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var validOrder = Order(validTenant.Id, inRange, 100m, 10m);
        var systemOrder = Order(systemTenant.Id, inRange, 200m, 0m);
        var deletedTenantOrder = Order(deletedTenant.Id, inRange, 300m, 0m);
        var cancelledOrder = Order(validTenant.Id, inRange, 400m, 0m, status: "Cancelled");
        var upperBoundaryOrder = Order(
            validTenant.Id,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            500m,
            0m);
        context.BillingOrders.AddRange(
            validOrder,
            systemOrder,
            deletedTenantOrder,
            cancelledOrder,
            upperBoundaryOrder);
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var rows = await repository.GetInvoicedOrdersAsync(
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            SystemAnalyticsSegment.All,
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(validOrder.Id, row.OrderId);
        Assert.Equal(90m, row.FinalAmount);
    }

    [Fact]
    public async Task CollectedPayments_UseProcessedAtAndKnownSuccessfulStatusesOnly()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Valid tenant");
        var systemTenant = Tenant("SYSTEM");
        context.Tenants.AddRange(tenant, systemTenant);
        var processedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var order = Order(tenant.Id, processedAt, 1000m, 0m);
        var systemOrder = Order(systemTenant.Id, processedAt, 1000m, 0m);
        context.BillingOrders.AddRange(order, systemOrder);
        context.PaymentTransactions.AddRange(
            Payment(tenant.Id, order.Id, "Success", processedAt, 100m, rawData: "secret-1"),
            Payment(tenant.Id, order.Id, "sEtTlEd", processedAt, 200m, rawData: "secret-2"),
            Payment(tenant.Id, order.Id, "Failed", processedAt, 300m),
            Payment(tenant.Id, order.Id, "Success", null, 400m),
            Payment(systemTenant.Id, systemOrder.Id, "Success", processedAt, 500m));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var rows = await repository.GetRevenuePaymentsAsync(
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            SystemAnalyticsSegment.All,
            CancellationToken.None);

        Assert.Equal(4, rows.Count);
        Assert.Equal(
            300m,
            rows
                .Where(row => row.ProcessedAt.HasValue
                    && (string.Equals(row.Status, "Success", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(row.Status, "Settled", StringComparison.OrdinalIgnoreCase)))
                .Sum(row => row.Amount));
        Assert.Null(typeof(SMEFLOWSystem.Application.Interfaces.IRepositories.CollectedPaymentRow)
            .GetProperty(nameof(PaymentTransaction.RawData)));
    }

    [Fact]
    public async Task ModuleFilterAndMrr_UseExistingLinesAndActiveSubscriptions()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Valid tenant");
        var systemTenant = Tenant("SYSTEM");
        context.Tenants.AddRange(tenant, systemTenant);
        var moduleA = Module("A", 100m);
        var moduleB = Module("B", 200m);
        context.Modules.AddRange(moduleA, moduleB);
        await context.SaveChangesAsync();

        var processedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var order = Order(tenant.Id, processedAt, 100m, 0m);
        context.BillingOrders.Add(order);
        context.BillingOrderModules.Add(new BillingOrderModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            BillingOrderId = order.Id,
            ModuleId = moduleA.Id,
            LineTotal = 100m,
            UnitPrice = 100m
        });
        context.PaymentTransactions.Add(
            Payment(tenant.Id, order.Id, "Paid", processedAt, 100m));
        context.ModuleSubscriptions.AddRange(
            Subscription(tenant.Id, moduleA.Id, "Active", processedAt.AddMonths(-1), processedAt.AddMonths(1)),
            Subscription(tenant.Id, moduleB.Id, "Trial", processedAt.AddMonths(-1), processedAt.AddMonths(1)),
            Subscription(systemTenant.Id, moduleB.Id, "Active", processedAt.AddMonths(-1), processedAt.AddMonths(1)));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var moduleAPayments = await repository.GetRevenuePaymentsAsync(
            processedAt.AddDays(-1),
            processedAt.AddDays(1),
            moduleA.Id,
            SystemAnalyticsSegment.All,
            CancellationToken.None);
        var moduleBPayments = await repository.GetRevenuePaymentsAsync(
            processedAt.AddDays(-1),
            processedAt.AddDays(1),
            moduleB.Id,
            SystemAnalyticsSegment.All,
            CancellationToken.None);
        var mrr = await repository.GetEstimatedMrrAtAsync(
            processedAt,
            null,
            SystemAnalyticsSegment.All,
            CancellationToken.None);

        Assert.Single(moduleAPayments);
        Assert.Empty(moduleBPayments);
        Assert.Equal(100m, mrr);
    }

    [Fact]
    public async Task ModuleAllocations_LoadOnlyLinesForCollectedPaymentOrderIds()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Valid tenant");
        context.Tenants.Add(tenant);
        var module = Module("ATTENDANCE", 100m);
        context.Modules.Add(module);
        await context.SaveChangesAsync();

        var requestedOrder = Order(
            tenant.Id,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            100m,
            10m);
        var unrelatedOrder = Order(
            tenant.Id,
            new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            200m,
            0m);
        context.BillingOrders.AddRange(requestedOrder, unrelatedOrder);
        context.BillingOrderModules.AddRange(
            BillingLine(tenant.Id, requestedOrder.Id, module.Id, 100m),
            BillingLine(tenant.Id, unrelatedOrder.Id, module.Id, 200m));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var rows = await repository.GetBillingOrderModuleAllocationsAsync(
            [requestedOrder.Id],
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(requestedOrder.Id, row.OrderId);
        Assert.Equal("ATTENDANCE", row.ModuleCode);
        Assert.Equal(90m, row.OrderFinalAmount);
        Assert.Equal(10m, row.OrderDiscountAmount);
    }

    [Fact]
    public async Task TenantFinancialAggregate_HandlesTenantWithoutPayments()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Valid tenant");
        context.Tenants.Add(tenant);
        context.BillingOrders.Add(Order(
            tenant.Id,
            new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            120m,
            20m));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var result = await repository.GetTenantFinancialAggregateAsync(
            tenant.Id,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.LifetimeCollectedRevenue);
        Assert.Equal(0m, result.CollectedRevenueInPeriod);
        Assert.Equal(100m, result.OutstandingAmount);
        Assert.Null(result.LastSuccessfulPaymentAt);
        Assert.Null(result.LastFailedPaymentAt);
        Assert.Empty(result.PaymentDelayDaysList);
    }

    [Fact]
    public async Task RevenueQueries_FilterPaidAndTrialTenantSegments()
    {
        await using var context = CreateContext();
        var paidTenant = Tenant("Paid tenant");
        var trialTenant = Tenant("Trial tenant");
        trialTenant.Status = "Trial";
        context.Tenants.AddRange(paidTenant, trialTenant);
        var billingDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var paidOrder = Order(paidTenant.Id, billingDate, 100m, 0m);
        var trialOrder = Order(trialTenant.Id, billingDate, 200m, 0m);
        context.BillingOrders.AddRange(paidOrder, trialOrder);
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var paidRows = await repository.GetInvoicedOrdersAsync(
            billingDate.AddDays(-1),
            billingDate.AddDays(1),
            null,
            SystemAnalyticsSegment.Paid,
            CancellationToken.None);
        var trialRows = await repository.GetInvoicedOrdersAsync(
            billingDate.AddDays(-1),
            billingDate.AddDays(1),
            null,
            SystemAnalyticsSegment.Trial,
            CancellationToken.None);

        Assert.Equal(paidOrder.Id, Assert.Single(paidRows).OrderId);
        Assert.Equal(trialOrder.Id, Assert.Single(trialRows).OrderId);
    }

    [Fact]
    public async Task RevenueSeriesAndEveryBreakdownDimension_ReconcileThroughReadRepository()
    {
        await using var context = CreateContext();
        var tenantA = Tenant("Tenant A");
        var tenantB = Tenant("Tenant B");
        var systemTenant = Tenant("SYSTEM");
        context.Tenants.AddRange(tenantA, tenantB, systemTenant);

        var moduleA = Module("ATTENDANCE", 100m);
        var moduleB = Module("PAYROLL", 200m);
        context.Modules.AddRange(moduleA, moduleB);
        await context.SaveChangesAsync();

        var processedAt = new DateTime(2026, 7, 15, 5, 0, 0, DateTimeKind.Utc);
        var orderA = Order(tenantA.Id, processedAt, 100m, 10m);
        var orderB = Order(tenantB.Id, processedAt.AddDays(1), 50m, 0m);
        var systemOrder = Order(systemTenant.Id, processedAt, 500m, 0m);
        context.BillingOrders.AddRange(orderA, orderB, systemOrder);
        context.BillingOrderModules.AddRange(
            BillingLine(tenantA.Id, orderA.Id, moduleA.Id, 60m),
            BillingLine(tenantA.Id, orderA.Id, moduleB.Id, 40m),
            BillingLine(tenantB.Id, orderB.Id, moduleB.Id, 50m),
            BillingLine(systemTenant.Id, systemOrder.Id, moduleA.Id, 500m));
        context.PaymentTransactions.AddRange(
            Payment(tenantA.Id, orderA.Id, "Success", processedAt, 90m),
            Payment(tenantB.Id, orderB.Id, "Settled", processedAt.AddDays(1), 50m),
            Payment(tenantA.Id, orderA.Id, "Failed", processedAt, 30m),
            Payment(systemTenant.Id, systemOrder.Id, "Paid", processedAt, 500m));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var service = new SystemAnalyticsService(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SystemAnalyticsOptions()),
            NullLogger<SystemAnalyticsService>.Instance);
        var series = await service.GetRevenueSeriesAsync(new SystemRevenueSeriesQueryDto
        {
            From = new DateOnly(2026, 7, 1),
            To = new DateOnly(2026, 7, 31),
            Compare = SystemAnalyticsCompare.None,
            Granularity = SystemAnalyticsGranularity.Day
        });
        var seriesCollected = series.Points.Sum(point => point.CollectedRevenue);

        Assert.Equal(140m, seriesCollected);
        foreach (var dimension in new[]
                 {
                     SystemAnalyticsDimension.Module,
                     SystemAnalyticsDimension.Tenant,
                     SystemAnalyticsDimension.Gateway
                 })
        {
            var breakdown = await service.GetRevenueBreakdownAsync(
                new SystemRevenueBreakdownQueryDto
                {
                    From = new DateOnly(2026, 7, 1),
                    To = new DateOnly(2026, 7, 31),
                    Compare = SystemAnalyticsCompare.None,
                    Dimension = dimension,
                    Limit = 10
                });

            Assert.Equal(seriesCollected, breakdown.TotalCollectedRevenue);
            Assert.Equal(
                seriesCollected,
                breakdown.Items.Sum(item => item.CollectedRevenue)
                    + (breakdown.Other?.CollectedRevenue ?? 0m));
        }
    }

    [Fact]
    public async Task ActionCenterCandidates_ApplyTimeBoundariesAndExcludeSystemTenant()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Tenant A");
        var suspendedTenant = Tenant("Suspended tenant");
        suspendedTenant.Status = "Suspended";
        var systemTenant = Tenant("SYSTEM");
        context.Tenants.AddRange(tenant, suspendedTenant, systemTenant);
        var module = Module("ATTENDANCE", 100m);
        context.Modules.Add(module);
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var failedBoundaryOrder = Order(tenant.Id, now, 100m, 0m);
        var failedOutsideOrder = Order(tenant.Id, now, 100m, 0m);
        var overdueBoundaryOrder = Order(tenant.Id, now.AddHours(-24), 100m, 0m);
        var notOverdueOrder = Order(
            tenant.Id,
            now.AddHours(-24).AddSeconds(1),
            100m,
            0m);
        var systemOrder = Order(systemTenant.Id, now, 100m, 0m);
        context.BillingOrders.AddRange(
            failedBoundaryOrder,
            failedOutsideOrder,
            overdueBoundaryOrder,
            notOverdueOrder,
            systemOrder);
        var failedBoundary = Payment(
            tenant.Id,
            failedBoundaryOrder.Id,
            "Failed",
            now.AddHours(-24),
            100m);
        var failedOutside = Payment(
            tenant.Id,
            failedOutsideOrder.Id,
            "Failed",
            now.AddHours(-24).AddTicks(-1),
            100m);
        var systemFailure = Payment(
            systemTenant.Id,
            systemOrder.Id,
            "Failed",
            now,
            100m);
        context.PaymentTransactions.AddRange(
            failedBoundary,
            failedOutside,
            systemFailure);
        var activeBoundary = Subscription(
            tenant.Id,
            module.Id,
            "Active",
            now.AddMonths(-1),
            now.AddDays(7));
        var trialBoundary = Subscription(
            tenant.Id,
            module.Id,
            "Trial",
            now.AddDays(-1),
            now.AddDays(7));
        var outsideWindow = Subscription(
            tenant.Id,
            module.Id,
            "Active",
            now.AddMonths(-1),
            now.AddDays(7).AddTicks(1));
        var systemSubscription = Subscription(
            systemTenant.Id,
            module.Id,
            "Active",
            now.AddMonths(-1),
            now.AddDays(1));
        context.ModuleSubscriptions.AddRange(
            activeBoundary,
            trialBoundary,
            outsideWindow,
            systemSubscription);
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var candidates = await repository.GetActionCenterCandidatesAsync(
            now,
            overdueGraceHours: 24,
            CancellationToken.None);

        Assert.Contains(candidates, candidate =>
            candidate.Type == SystemActionCenterItemType.PaymentFailed
            && candidate.EntityId == failedBoundary.Id);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.EntityId == failedOutside.Id);
        Assert.Contains(candidates, candidate =>
            candidate.Type == SystemActionCenterItemType.OrderOverdue
            && candidate.EntityId == overdueBoundaryOrder.Id);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.EntityId == notOverdueOrder.Id);
        Assert.Contains(candidates, candidate =>
            candidate.Type == SystemActionCenterItemType.SubscriptionExpiring
            && candidate.EntityId == activeBoundary.Id);
        Assert.Contains(candidates, candidate =>
            candidate.Type == SystemActionCenterItemType.TrialEnding
            && candidate.EntityId == trialBoundary.Id);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.EntityId == outsideWindow.Id);
        Assert.Contains(candidates, candidate =>
            candidate.Type == SystemActionCenterItemType.TenantSuspended
            && candidate.EntityId == suspendedTenant.Id);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.TenantId == systemTenant.Id);
    }

    private static SMEFLOWSystemContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SMEFLOWSystemContext>()
            .UseInMemoryDatabase($"system-analytics-{Guid.NewGuid():N}")
            .Options;
        return new SMEFLOWSystemContext(options, new FakeCurrentTenantService());
    }

    private static Tenant Tenant(string name, bool isDeleted = false)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted
        };
    }

    private static BillingOrder Order(
        Guid tenantId,
        DateTime billingDate,
        decimal total,
        decimal discount,
        string status = "Pending")
    {
        return new BillingOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BillingOrderNumber = $"BO-{Guid.NewGuid():N}",
            BillingDate = billingDate,
            TotalAmount = total,
            DiscountAmount = discount,
            FinalAmount = null,
            PaymentStatus = "Pending",
            Status = status,
            CreatedAt = billingDate,
            IsDeleted = false
        };
    }

    private static PaymentTransaction Payment(
        Guid tenantId,
        Guid orderId,
        string status,
        DateTime? processedAt,
        decimal amount,
        string? rawData = null)
    {
        return new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BillingOrderId = orderId,
            Gateway = "Test",
            GatewayTransactionId = Guid.NewGuid().ToString("N"),
            Amount = amount,
            Status = status,
            RawData = rawData,
            CreatedAt = processedAt ?? DateTime.UtcNow,
            ProcessedAt = processedAt
        };
    }

    private static BillingOrderModule BillingLine(
        Guid tenantId,
        Guid orderId,
        int moduleId,
        decimal lineTotal)
    {
        return new BillingOrderModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BillingOrderId = orderId,
            ModuleId = moduleId,
            LineTotal = lineTotal,
            UnitPrice = lineTotal
        };
    }

    private static Module Module(string code, decimal monthlyPrice)
    {
        return new Module
        {
            Code = code,
            ShortCode = code,
            Name = $"Module {code}",
            Description = string.Empty,
            MonthlyPrice = monthlyPrice,
            IsActive = true
        };
    }

    private static ModuleSubscription Subscription(
        Guid tenantId,
        int moduleId,
        string status,
        DateTime startDate,
        DateTime endDate)
    {
        return new ModuleSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            Status = status,
            StartDate = startDate,
            EndDate = endDate,
            IsDeleted = false
        };
    }

    private sealed class FakeCurrentTenantService : ICurrentTenantService
    {
        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }
}
