using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
