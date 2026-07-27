using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
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
            null,
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
    public async Task TenantFinancialAggregate_ReconcilesPaymentsOutstandingAndModuleFilter()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Tenant A");
        context.Tenants.Add(tenant);
        var moduleA = Module("A", 100m);
        var moduleB = Module("B", 200m);
        context.Modules.AddRange(moduleA, moduleB);
        await context.SaveChangesAsync();

        var paidInPeriodOrder = Order(
            tenant.Id,
            new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            100m,
            10m);
        paidInPeriodOrder.PaymentStatus = "Paid";
        var paidOutsidePeriodOrder = Order(
            tenant.Id,
            new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            50m,
            0m);
        paidOutsidePeriodOrder.PaymentStatus = "Paid";
        var negativeDelayOrder = Order(
            tenant.Id,
            new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
            30m,
            0m);
        negativeDelayOrder.PaymentStatus = "Paid";
        var failedOrder = Order(
            tenant.Id,
            new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
            40m,
            0m);
        failedOrder.PaymentStatus = "Failed";
        var pendingOrder = Order(
            tenant.Id,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            120m,
            20m);
        context.BillingOrders.AddRange(
            paidInPeriodOrder,
            paidOutsidePeriodOrder,
            negativeDelayOrder,
            failedOrder,
            pendingOrder);
        context.BillingOrderModules.AddRange(
            BillingLine(tenant.Id, paidInPeriodOrder.Id, moduleA.Id, 100m),
            BillingLine(tenant.Id, paidOutsidePeriodOrder.Id, moduleB.Id, 50m),
            BillingLine(tenant.Id, negativeDelayOrder.Id, moduleA.Id, 30m),
            BillingLine(tenant.Id, failedOrder.Id, moduleA.Id, 40m),
            BillingLine(tenant.Id, pendingOrder.Id, moduleA.Id, 120m));
        context.PaymentTransactions.AddRange(
            Payment(
                tenant.Id,
                paidInPeriodOrder.Id,
                "Success",
                new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
                90m),
            Payment(
                tenant.Id,
                paidOutsidePeriodOrder.Id,
                "Settled",
                new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
                50m),
            Payment(
                tenant.Id,
                negativeDelayOrder.Id,
                "Paid",
                new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc),
                30m),
            Payment(
                tenant.Id,
                failedOrder.Id,
                "Failed",
                new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
                40m));
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var allModules = await repository.GetTenantFinancialAggregateAsync(
            tenant.Id,
            from,
            toExclusive,
            null,
            CancellationToken.None);
        var moduleAOnly = await repository.GetTenantFinancialAggregateAsync(
            tenant.Id,
            from,
            toExclusive,
            moduleA.Id,
            CancellationToken.None);

        Assert.NotNull(allModules);
        Assert.Equal(170m, allModules.LifetimeCollectedRevenue);
        Assert.Equal(120m, allModules.CollectedRevenueInPeriod);
        Assert.Equal(100m, allModules.OutstandingAmount);
        Assert.Equal(
            new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc),
            allModules.LastSuccessfulPaymentAt);
        Assert.Equal(
            new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
            allModules.LastFailedPaymentAt);
        Assert.Contains(-1m, allModules.PaymentDelayDaysList);

        Assert.NotNull(moduleAOnly);
        Assert.Equal(120m, moduleAOnly.LifetimeCollectedRevenue);
        Assert.Equal(120m, moduleAOnly.CollectedRevenueInPeriod);
        Assert.Equal(100m, moduleAOnly.OutstandingAmount);
    }

    [Fact]
    public async Task TenantSubscriptionSummary_CountsCurrentRowsAndEstimatedMrrInOneProjection()
    {
        await using var context = CreateContext();
        var tenant = Tenant("Tenant A");
        context.Tenants.Add(tenant);
        var moduleA = Module("A", 100m);
        var moduleB = Module("B", 200m);
        context.Modules.AddRange(moduleA, moduleB);
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        context.ModuleSubscriptions.AddRange(
            Subscription(tenant.Id, moduleA.Id, "Active", now.AddDays(-1), now.AddDays(10)),
            Subscription(tenant.Id, moduleB.Id, "Trial", now.AddDays(-1), now.AddDays(20)),
            Subscription(tenant.Id, moduleB.Id, "Active", now.AddDays(-1), now.AddDays(60)),
            Subscription(tenant.Id, moduleA.Id, "Active", now.AddDays(-10), now.AddDays(-1)),
            new ModuleSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ModuleId = moduleA.Id,
                Status = "Active",
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(5),
                IsDeleted = true
            });
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var allModules = await repository.GetTenantSubscriptionCountsAsync(
            tenant.Id,
            now,
            null,
            CancellationToken.None);
        var moduleAOnly = await repository.GetTenantSubscriptionCountsAsync(
            tenant.Id,
            now,
            moduleA.Id,
            CancellationToken.None);

        Assert.Equal(2, allModules.Active);
        Assert.Equal(1, allModules.Trial);
        Assert.Equal(2, allModules.ExpiringIn30Days);
        Assert.Equal(300m, allModules.EstimatedMrr);
        Assert.Equal(1, moduleAOnly.Active);
        Assert.Equal(0, moduleAOnly.Trial);
        Assert.Equal(1, moduleAOnly.ExpiringIn30Days);
        Assert.Equal(100m, moduleAOnly.EstimatedMrr);
    }

    [Fact]
    public async Task TenantFinancialAggregate_ReturnsNullForMissingDeletedAndSystemTenant()
    {
        await using var context = CreateContext();
        var deletedTenant = Tenant("Deleted tenant", isDeleted: true);
        var systemTenant = Tenant("SYSTEM");
        context.Tenants.AddRange(deletedTenant, systemTenant);
        await context.SaveChangesAsync();

        var repository = new SystemAnalyticsReadRepository(context);
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Null(await repository.GetTenantFinancialAggregateAsync(
            Guid.NewGuid(),
            from,
            toExclusive,
            null,
            CancellationToken.None));
        Assert.Null(await repository.GetTenantFinancialAggregateAsync(
            deletedTenant.Id,
            from,
            toExclusive,
            null,
            CancellationToken.None));
        Assert.Null(await repository.GetTenantFinancialAggregateAsync(
            systemTenant.Id,
            from,
            toExclusive,
            null,
            CancellationToken.None));
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
