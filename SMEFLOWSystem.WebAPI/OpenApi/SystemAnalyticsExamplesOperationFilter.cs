using System.Text.Json;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.WebAPI.Controllers.System;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SMEFLOWSystem.WebAPI.OpenApi;

public sealed class SystemAnalyticsExamplesOperationFilter : IOperationFilter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var example = CreateExample(
            context.MethodInfo.DeclaringType,
            context.MethodInfo.Name);
        if (example is null
            || !operation.Responses.TryGetValue("200", out var response))
        {
            return;
        }

        if (response.Content.Count == 0)
        {
            response.Content["application/json"] = new OpenApiMediaType();
        }

        foreach (var content in response.Content.Where(item =>
                     item.Key.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            content.Value.Example = OpenApiAnyFactory.CreateFromJson(
                JsonSerializer.Serialize(example, JsonOptions));
        }
    }

    private static object? CreateExample(Type? controllerType, string methodName)
    {
        if (controllerType == typeof(SystemAnalyticsController))
        {
            return methodName switch
            {
                nameof(SystemAnalyticsController.GetRevenueSeries) =>
                    RevenueSeriesExample(),
                nameof(SystemAnalyticsController.GetRevenueBreakdown) =>
                    RevenueBreakdownExample(),
                nameof(SystemAnalyticsController.GetActionCenter) =>
                    ActionCenterExample(),
                nameof(SystemAnalyticsController.GetRevenueForecast) =>
                    RevenueForecastExample(),
                _ => null
            };
        }

        if (controllerType == typeof(SystemTenantAnalyticsController)
            && methodName
                == nameof(SystemTenantAnalyticsController.GetFinancialSummary))
        {
            return TenantFinancialExample();
        }

        if (controllerType == typeof(SystemOperationsController)
            && methodName == nameof(SystemOperationsController.GetHealthSummary))
        {
            return OperationsHealthExample();
        }

        return null;
    }

    private static SystemRevenueSeriesResponseDto RevenueSeriesExample()
    {
        return new SystemRevenueSeriesResponseDto
        {
            Points =
            [
                new SystemRevenueSeriesPointDto
                {
                    BucketStart = "2026-07-01",
                    InvoicedRevenue = 1_200_000m,
                    CollectedRevenue = 1_000_000m,
                    RefundedAmount = null,
                    OutstandingCreated = 200_000m,
                    MrrSnapshot = 600_000m
                }
            ],
            PreviousPoints = null,
            Meta = Meta(SystemAnalyticsMrrStatus.Estimated)
        };
    }

    private static SystemRevenueBreakdownResponseDto RevenueBreakdownExample()
    {
        return new SystemRevenueBreakdownResponseDto
        {
            TotalCollectedRevenue = 1_000_000m,
            Items =
            [
                new SystemRevenueBreakdownItemDto
                {
                    Id = "ATTENDANCE",
                    Name = "Attendance",
                    CollectedRevenue = 750_000m,
                    PercentageOfTotal = 75m
                }
            ],
            Other = new SystemRevenueBreakdownItemDto
            {
                Id = "OTHER",
                Name = "Other",
                CollectedRevenue = 250_000m,
                PercentageOfTotal = 25m
            },
            Meta = Meta(SystemAnalyticsMrrStatus.Unavailable)
        };
    }

    private static SystemActionCenterResponseDto ActionCenterExample()
    {
        const string entityId = "11111111-1111-1111-1111-111111111111";
        return new SystemActionCenterResponseDto
        {
            Counts = new SystemActionCenterCountsDto
            {
                Critical = 1,
                Warning = 0,
                Info = 0
            },
            Items =
            [
                new SystemActionCenterItemDto
                {
                    Id = $"{SystemActionCenterItemType.OrderOverdue}_{entityId}",
                    Type = SystemActionCenterItemType.OrderOverdue,
                    Severity = SystemActionCenterSeverity.Critical,
                    Title = "Overdue billing order",
                    Description = "A billing order requires attention.",
                    OccurredAt = ExampleUtc(),
                    EntityId = Guid.Parse(entityId),
                    TargetPath = $"/system-admin/billing-orders/{entityId}"
                }
            ],
            Meta = Meta(SystemAnalyticsMrrStatus.Unavailable)
        };
    }

    private static SystemTenantFinancialSummaryResponseDto TenantFinancialExample()
    {
        return new SystemTenantFinancialSummaryResponseDto
        {
            TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenantName = "Example tenant",
            Status = "Active",
            CurrentMrr = 600_000m,
            LifetimeCollectedRevenue = 12_000_000m,
            CollectedRevenueInPeriod = 1_000_000m,
            OutstandingAmount = 200_000m,
            LastSuccessfulPaymentAt = ExampleUtc(),
            LastFailedPaymentAt = null,
            AveragePaymentDelayDays = 1.5d,
            Subscriptions = new SystemTenantSubscriptionSummaryDto
            {
                Active = 3,
                Trial = 0,
                ExpiringIn30Days = 1
            },
            Meta = Meta(SystemAnalyticsMrrStatus.Estimated)
        };
    }

    private static SystemOperationsHealthResponseDto OperationsHealthExample()
    {
        return new SystemOperationsHealthResponseDto
        {
            Status = "Healthy",
            CheckedAt = ExampleUtc(),
            DurationMs = 12,
            Components =
            [
                new SystemOperationsHealthComponentDto
                {
                    Name = "postgres",
                    Status = "Healthy",
                    DurationMs = 5,
                    Description = "PostgreSQL database is reachable."
                }
            ]
        };
    }

    private static SystemRevenueForecastResponseDto RevenueForecastExample()
    {
        var meta = Meta(SystemAnalyticsMrrStatus.Unavailable);
        meta.From = "2026-01-01";
        meta.To = "2026-08-31";
        meta.Warnings =
        [
            SystemAnalyticsWarningCodes.ForecastExcludesRefunds,
            SystemAnalyticsWarningCodes.ForecastBasedOnAvailablePaymentHistory
        ];
        return new SystemRevenueForecastResponseDto
        {
            Method = "LinearTrend",
            TrainingFrom = "2026-01-01",
            TrainingTo = "2026-06-30",
            Currency = "VND",
            Granularity = "month",
            ActualPoints =
            [
                new SystemRevenueForecastActualPointDto
                {
                    BucketStart = "2026-06-01",
                    Value = 1_000_000m
                }
            ],
            ForecastPoints =
            [
                new SystemRevenueForecastPointDto
                {
                    BucketStart = "2026-07-01",
                    Value = 1_100_000m,
                    LowerBound = 950_000m,
                    UpperBound = 1_250_000m
                }
            ],
            Meta = meta
        };
    }

    private static SystemAnalyticsMetaDto Meta(string mrrStatus)
    {
        return new SystemAnalyticsMetaDto
        {
            From = "2026-07-01",
            To = "2026-07-31",
            Timezone = "Asia/Ho_Chi_Minh",
            Currency = "VND",
            GeneratedAt = ExampleUtc(),
            DataThrough = ExampleUtc(),
            Freshness = "Live",
            ExcludesInternalTenant = true,
            ExcludesTestTenants = false,
            MrrStatus = mrrStatus,
            Warnings = [SystemAnalyticsWarningCodes.TestTenantFlagUnavailable]
        };
    }

    private static DateTime ExampleUtc()
    {
        return new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc);
    }
}
