using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.Authorization;
using SMEFLOWSystem.WebAPI.Controllers.System;
using SMEFLOWSystem.WebAPI.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SMEFLOWSystem.Tests;

public sealed class SystemAnalyticsPhaseNineTests
{
    private static readonly EndpointContract[] Endpoints =
    [
        Endpoint<SystemAnalyticsController>(
            nameof(SystemAnalyticsController.GetRevenueSeries),
            typeof(SystemRevenueSeriesResponseDto)),
        Endpoint<SystemAnalyticsController>(
            nameof(SystemAnalyticsController.GetRevenueBreakdown),
            typeof(SystemRevenueBreakdownResponseDto)),
        Endpoint<SystemAnalyticsController>(
            nameof(SystemAnalyticsController.GetActionCenter),
            typeof(SystemActionCenterResponseDto)),
        Endpoint<SystemAnalyticsController>(
            nameof(SystemAnalyticsController.GetRevenueForecast),
            typeof(SystemRevenueForecastResponseDto)),
        Endpoint<SystemTenantAnalyticsController>(
            nameof(SystemTenantAnalyticsController.GetFinancialSummary),
            typeof(SystemTenantFinancialSummaryResponseDto)),
        Endpoint<SystemOperationsController>(
            nameof(SystemOperationsController.GetHealthSummary),
            typeof(SystemOperationsHealthResponseDto))
    ];

    [Fact]
    public void SixEndpoints_RequireSystemAdminAndExposeCancellation()
    {
        Assert.Equal(6, Endpoints.Length);
        foreach (var endpoint in Endpoints)
        {
            var authorize = endpoint.ControllerType
                .GetCustomAttribute<AuthorizeAttribute>();
            var allowsAnonymous =
                endpoint.ControllerType.IsDefined(
                    typeof(AllowAnonymousAttribute),
                    inherit: true)
                || endpoint.Method.IsDefined(
                    typeof(AllowAnonymousAttribute),
                    inherit: true);
            var responseTypes = endpoint.Method
                .GetCustomAttributes<ProducesResponseTypeAttribute>()
                .ToList();

            Assert.Equal(PolicyNames.SystemAdmin, authorize?.Policy);
            Assert.False(allowsAnonymous);
            Assert.Contains(
                endpoint.Method.GetParameters(),
                parameter =>
                    parameter.ParameterType == typeof(CancellationToken));
            Assert.Contains(responseTypes, response =>
                response.StatusCode == 200
                && response.Type == endpoint.ResponseType);
        }
    }

    [Fact]
    public async Task ActiveSystemAdminRequirement_RejectsMissingOrInactiveIdentity()
    {
        var requirement = new ActiveSystemAdminRequirement();
        var repository = new StubBootstrapRepository();
        var handler = new ActiveSystemAdminHandler(repository);
        var missingIdentity = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null);

        await handler.HandleAsync(missingIdentity);

        Assert.False(missingIdentity.HasSucceeded);
        Assert.Equal(0, repository.LookupCount);

        var userId = Guid.NewGuid();
        var inactive = AuthorizationContext(requirement, userId);
        await handler.HandleAsync(inactive);

        Assert.False(inactive.HasSucceeded);
        Assert.Equal(1, repository.LookupCount);
        Assert.Equal(userId, repository.ObservedUserId);
    }

    [Fact]
    public async Task ActiveSystemAdminRequirement_AllowsActiveIdentity()
    {
        var requirement = new ActiveSystemAdminRequirement();
        var repository = new StubBootstrapRepository
        {
            IsActiveSystemAdmin = true
        };
        var context = AuthorizationContext(requirement, Guid.NewGuid());

        await new ActiveSystemAdminHandler(repository).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public void SixEndpoints_HaveSwaggerExamplesAndCanonicalErrorSchemas()
    {
        foreach (var endpoint in Endpoints)
        {
            var context = OperationContext(endpoint.Method);
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse
                    {
                        Description = "Success",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new()
                        }
                    }
                }
            };

            new SystemAnalyticsExamplesOperationFilter().Apply(
                operation,
                context);
            new ProblemDetailsResponseOperationFilter().Apply(
                operation,
                context);

            Assert.NotNull(
                operation.Responses["200"]
                    .Content["application/json"]
                    .Example);
            foreach (var status in new[] { "400", "401", "403", "500" })
            {
                Assert.True(operation.Responses.ContainsKey(status));
                Assert.Contains(
                    "application/problem+json",
                    operation.Responses[status].Content.Keys);
            }

            if (endpoint.Method.Name
                == nameof(SystemAnalyticsController.GetRevenueForecast))
            {
                Assert.Contains(
                    "application/problem+json",
                    operation.Responses["422"].Content.Keys);
            }
            else
            {
                Assert.DoesNotContain("422", operation.Responses.Keys);
            }
        }
    }

    [Fact]
    public void AnalyticsResponseDtos_DoNotExposeSensitiveSourceFields()
    {
        var dtoTypes = typeof(SystemAnalyticsMetaDto).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace
                    == typeof(SystemAnalyticsMetaDto).Namespace
                && type.Name.StartsWith("System", StringComparison.Ordinal)
                && type.Name.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();
        var propertyNames = dtoTypes
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .ToList();
        var forbiddenFragments = new[]
        {
            "RawData",
            "Password",
            "Secret",
            "ConnectionString",
            "ExternalUrl",
            "CallbackUrl"
        };

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(propertyNames, name =>
                name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void RemainingResponses_SerializeCanonicalNamesNullsAndWarnings()
    {
        var meta = new SystemAnalyticsMetaDto
        {
            From = "2026-07-01",
            To = "2026-07-31",
            Timezone = "Asia/Ho_Chi_Minh",
            Currency = "VND",
            MrrStatus = SystemAnalyticsMrrStatus.Estimated,
            Warnings = [SystemAnalyticsWarningCodes.TestTenantFlagUnavailable]
        };
        var tenant = JsonSerializer.SerializeToElement(
            new SystemTenantFinancialSummaryResponseDto
            {
                TenantId = Guid.NewGuid(),
                TenantName = "Tenant A",
                Status = "Active",
                LastSuccessfulPaymentAt = null,
                LastFailedPaymentAt = null,
                AveragePaymentDelayDays = null,
                Meta = meta
            },
            WebJsonOptions());
        var health = JsonSerializer.SerializeToElement(
            new SystemOperationsHealthResponseDto
            {
                Status = "Healthy",
                Components =
                [
                    new SystemOperationsHealthComponentDto
                    {
                        Name = "postgres",
                        Status = "Healthy",
                        Description = null
                    }
                ]
            },
            WebJsonOptions());
        var forecast = JsonSerializer.SerializeToElement(
            new SystemRevenueForecastResponseDto
            {
                ActualPoints =
                [
                    new SystemRevenueForecastActualPointDto
                    {
                        BucketStart = "2026-06-01",
                        Value = 100m
                    }
                ],
                ForecastPoints =
                [
                    new SystemRevenueForecastPointDto
                    {
                        BucketStart = "2026-07-01",
                        Value = 110m,
                        LowerBound = 90m,
                        UpperBound = 130m
                    }
                ],
                Meta = meta
            },
            WebJsonOptions());

        Assert.Equal(
            JsonValueKind.Null,
            tenant.GetProperty("lastSuccessfulPaymentAt").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            tenant.GetProperty("averagePaymentDelayDays").ValueKind);
        Assert.False(tenant.TryGetProperty("TenantId", out _));
        Assert.Equal(
            JsonValueKind.Null,
            health.GetProperty("components")[0]
                .GetProperty("description")
                .ValueKind);
        Assert.Equal(
            90m,
            forecast.GetProperty("forecastPoints")[0]
                .GetProperty("lowerBound")
                .GetDecimal());
        Assert.Equal(
            SystemAnalyticsWarningCodes.TestTenantFlagUnavailable,
            forecast.GetProperty("meta")
                .GetProperty("warnings")[0]
                .GetString());
    }

    private static EndpointContract Endpoint<TController>(
        string methodName,
        Type responseType)
    {
        var method = typeof(TController).GetMethod(methodName);
        return new EndpointContract(
            typeof(TController),
            Assert.IsAssignableFrom<MethodInfo>(method),
            responseType);
    }

    private static OperationFilterContext OperationContext(MethodInfo method)
    {
        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(WebJsonOptions()));
        return new OperationFilterContext(
            new ApiDescription(),
            schemaGenerator,
            new SchemaRepository(),
            method);
    }

    private static JsonSerializerOptions WebJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    private static AuthorizationHandlerContext AuthorizationContext(
        ActiveSystemAdminRequirement requirement,
        Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleConstants.SystemAdmin)
            ],
            authenticationType: "phase-9-test");
        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);
    }

    private sealed record EndpointContract(
        Type ControllerType,
        MethodInfo Method,
        Type ResponseType);

    private sealed class StubBootstrapRepository :
        ISystemBootstrapResetRepository
    {
        public bool IsActiveSystemAdmin { get; init; }
        public int LookupCount { get; private set; }
        public Guid? ObservedUserId { get; private set; }

        public Task<bool> IsActiveSystemAdminAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            LookupCount++;
            ObservedUserId = userId;
            return Task.FromResult(IsActiveSystemAdmin);
        }

        public Task<SystemBootstrapResetTarget?> FindResetTargetAsync(
            Guid actorUserId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SystemBootstrapDependencyCounts> GetDependencyCountsAsync(
            SystemBootstrapResetTarget target,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteBootstrapIdentityAsync(
            SystemBootstrapResetTarget target,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
