using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
}
