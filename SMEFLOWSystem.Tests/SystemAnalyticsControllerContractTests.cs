using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using Microsoft.Extensions.Logging.Abstractions;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.Controllers.System;
using SMEFLOWSystem.WebAPI.ProblemDetails;

namespace SMEFLOWSystem.Tests;

public sealed class SystemAnalyticsControllerContractTests
{
    [Fact]
    public void RevenueSeriesRoute_RequiresSystemAdminPolicy()
    {
        var controllerType = typeof(SystemAnalyticsController);
        var route = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var action = controllerType.GetMethod(nameof(SystemAnalyticsController.GetRevenueSeries));
        var httpGet = action?.GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal("api/system/analytics", route?.Template);
        Assert.Equal(PolicyNames.SystemAdmin, authorize?.Policy);
        Assert.Equal("revenue-series", httpGet?.Template);
        Assert.NotNull(controllerType.GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void RevenueBreakdownRoute_UsesSystemAdminControllerPolicy()
    {
        var controllerType = typeof(SystemAnalyticsController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var action = controllerType.GetMethod(
            nameof(SystemAnalyticsController.GetRevenueBreakdown));
        var httpGet = action?.GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal(PolicyNames.SystemAdmin, authorize?.Policy);
        Assert.Equal("revenue-breakdown", httpGet?.Template);
    }

    [Fact]
    public void ActionCenterRoute_UsesSystemAdminControllerPolicy()
    {
        var controllerType = typeof(SystemAnalyticsController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var action = controllerType.GetMethod(
            nameof(SystemAnalyticsController.GetActionCenter));
        var httpGet = action?.GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal(PolicyNames.SystemAdmin, authorize?.Policy);
        Assert.Equal("action-center", httpGet?.Template);
        Assert.Contains(
            action!.GetCustomAttributes<ProducesResponseTypeAttribute>(),
            attribute =>
                attribute.StatusCode == StatusCodes.Status200OK
                && attribute.Type == typeof(SystemActionCenterResponseDto));
    }

    [Fact]
    public void TenantFinancialSummaryRoute_UsesSystemAdminPolicyAndContract()
    {
        var controllerType = typeof(SystemTenantAnalyticsController);
        var route = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var action = controllerType.GetMethod(
            nameof(SystemTenantAnalyticsController.GetFinancialSummary));
        var httpGet = action?.GetCustomAttribute<HttpGetAttribute>();
        var responses = action?.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .ToList();

        Assert.Equal("api/system/analytics/tenants", route?.Template);
        Assert.Equal(PolicyNames.SystemAdmin, authorize?.Policy);
        Assert.Equal("{tenantId:guid}/financial-summary", httpGet?.Template);
        Assert.Contains(responses!, response =>
            response.StatusCode == StatusCodes.Status200OK
            && response.Type == typeof(SystemTenantFinancialSummaryResponseDto));
        Assert.Contains(responses!, response =>
            response.StatusCode == StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TenantFinancialSummary_InvalidTenantReturnsSanitized404()
    {
        var controller = new SystemTenantAnalyticsController(
            new MissingTenantAnalyticsService(),
            NullLogger<SystemTenantAnalyticsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "phase-6-trace"
                }
            }
        };

        var result = await controller.GetFinancialSummary(
            Guid.NewGuid(),
            new SystemAnalyticsPeriodQueryDto(),
            CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.Equal("phase-6-trace", problem.Extensions["traceId"]);
        Assert.DoesNotContain("SYSTEM", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyticsProblemDetails_HasTraceIdAndSanitizedInternalError()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "phase-3-trace"
        };

        var validation = SystemAnalyticsProblemDetailsFactory.Validation(
            context,
            new Dictionary<string, string[]>
            {
                ["ModuleId"] = ["Module does not exist."]
            });
        var unexpected = SystemAnalyticsProblemDetailsFactory.UnexpectedError(context);

        Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
        Assert.Equal(StatusCodes.Status500InternalServerError, unexpected.Status);
        Assert.Equal("phase-3-trace", validation.Extensions["traceId"]);
        Assert.Equal("phase-3-trace", unexpected.Extensions["traceId"]);
        Assert.DoesNotContain("SQL", unexpected.Detail!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gateway", unexpected.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RevenueSeriesResponse_SerializesDocumentedCamelCaseContractAndNulls()
    {
        var response = new SystemRevenueSeriesResponseDto
        {
            Points =
            [
                new SystemRevenueSeriesPointDto
                {
                    BucketStart = "2026-07-01",
                    InvoicedRevenue = 100m,
                    CollectedRevenue = 80m,
                    RefundedAmount = null,
                    OutstandingCreated = 20m,
                    MrrSnapshot = 50m
                }
            ],
            PreviousPoints = null,
            Meta = ContractMeta(SystemAnalyticsMrrStatus.Estimated)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = document.RootElement;
        var point = root.GetProperty("points")[0];

        Assert.Equal(JsonValueKind.Null, root.GetProperty("previousPoints").ValueKind);
        Assert.Equal("2026-07-01", point.GetProperty("bucketStart").GetString());
        Assert.Equal(JsonValueKind.Null, point.GetProperty("refundedAmount").ValueKind);
        Assert.Equal(50m, point.GetProperty("mrrSnapshot").GetDecimal());
        Assert.Equal(
            SystemAnalyticsMrrStatus.Estimated,
            root.GetProperty("meta").GetProperty("mrrStatus").GetString());
        Assert.False(root.TryGetProperty("Points", out _));
    }

    [Fact]
    public void RevenueBreakdownResponse_SerializesDocumentedCamelCaseContract()
    {
        var response = new SystemRevenueBreakdownResponseDto
        {
            TotalCollectedRevenue = 100m,
            Items =
            [
                new SystemRevenueBreakdownItemDto
                {
                    Id = "ATTENDANCE",
                    Name = "Chấm công",
                    CollectedRevenue = 75m,
                    PercentageOfTotal = 75m
                }
            ],
            Other = new SystemRevenueBreakdownItemDto
            {
                Id = "OTHER",
                Name = "Khác",
                CollectedRevenue = 25m,
                PercentageOfTotal = 25m
            },
            Meta = ContractMeta(SystemAnalyticsMrrStatus.Unavailable)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = document.RootElement;

        Assert.Equal(100m, root.GetProperty("totalCollectedRevenue").GetDecimal());
        Assert.Equal("ATTENDANCE", root.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal(25m, root.GetProperty("other").GetProperty("collectedRevenue").GetDecimal());
        Assert.Equal(
            SystemAnalyticsMrrStatus.Unavailable,
            root.GetProperty("meta").GetProperty("mrrStatus").GetString());
        Assert.False(root.TryGetProperty("TotalCollectedRevenue", out _));
    }

    [Fact]
    public void ActionCenterResponse_SerializesDocumentedCamelCaseContract()
    {
        var entityId = Guid.NewGuid();
        var response = new SystemActionCenterResponseDto
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
                    Id = $"{SystemActionCenterItemType.OrderOverdue}_{entityId:D}",
                    Type = SystemActionCenterItemType.OrderOverdue,
                    Severity = SystemActionCenterSeverity.Critical,
                    Title = "Hóa đơn quá hạn thanh toán",
                    Description = "Hóa đơn đã quá hạn thanh toán.",
                    OccurredAt = new DateTime(
                        2026,
                        7,
                        24,
                        7,
                        0,
                        0,
                        DateTimeKind.Utc),
                    EntityId = entityId,
                    TargetPath = $"/system-admin/billing-orders/{entityId:D}"
                }
            ],
            Meta = ContractMeta(SystemAnalyticsMrrStatus.Unavailable)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = document.RootElement;
        var item = root.GetProperty("items")[0];

        Assert.Equal(1, root.GetProperty("counts").GetProperty("critical").GetInt32());
        Assert.Equal(
            SystemActionCenterItemType.OrderOverdue,
            item.GetProperty("type").GetString());
        Assert.Equal(
            $"/system-admin/billing-orders/{entityId:D}",
            item.GetProperty("targetPath").GetString());
        Assert.False(root.TryGetProperty("Counts", out _));
    }

    private static SystemAnalyticsMetaDto ContractMeta(string mrrStatus)
    {
        return new SystemAnalyticsMetaDto
        {
            From = "2026-07-01",
            To = "2026-07-31",
            Timezone = "Asia/Ho_Chi_Minh",
            Currency = "VND",
            GeneratedAt = new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc),
            Freshness = "Live",
            ExcludesInternalTenant = true,
            ExcludesTestTenants = false,
            MrrStatus = mrrStatus
        };
    }
    private sealed class MissingTenantAnalyticsService :
        ISystemTenantAnalyticsService
    {
        public Task<SystemTenantFinancialSummaryResponseDto>
            GetTenantFinancialSummaryAsync(
                Guid tenantId,
                SystemAnalyticsPeriodQueryDto query,
                CancellationToken ct = default)
        {
            throw new KeyNotFoundException("Tenant not found.");
        }
    }
}
