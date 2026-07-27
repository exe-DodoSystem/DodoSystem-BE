using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.WebAPI.Controllers;
using SMEFLOWSystem.WebAPI.Exceptions;
using SMEFLOWSystem.WebAPI.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SMEFLOWSystem.Tests;

public sealed class ReleaseReadinessContractTests
{
    private static readonly string[] CommonProblemStatuses =
        ["400", "404", "409", "500", "502", "503"];

    [Fact]
    [Trait("Phase", "8")]
    public void OpenApi_SecuredEndpointDocumentsProblemDetailsContract()
    {
        var operation = ApplyFilter(nameof(ContractProbeController.Secured));

        AssertProblemResponses(
            operation,
            CommonProblemStatuses.Concat(["401", "403"]));
    }

    [Fact]
    [Trait("Phase", "8")]
    public void OpenApi_AnonymousEndpointDoesNotAdvertiseAuthFailures()
    {
        var operation = ApplyFilter(nameof(ContractProbeController.Anonymous));

        AssertProblemResponses(operation, CommonProblemStatuses);
        Assert.DoesNotContain("401", operation.Responses.Keys);
        Assert.DoesNotContain("403", operation.Responses.Keys);
    }

    [Fact]
    [Trait("Phase", "8")]
    public void AttendanceTransportsShareClientRequestIdContract()
    {
        var jsonAction = typeof(AttendanceController).GetMethod(
            nameof(AttendanceController.SubmitPunch));
        var multipartAction = typeof(AttendanceController).GetMethod(
            nameof(AttendanceController.SubmitPunchForm));

        Assert.NotNull(jsonAction);
        Assert.NotNull(multipartAction);
        var jsonRequest = Assert.Single(
            jsonAction!.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(SubmitPunchRequestDto));
        var multipartRequest = Assert.Single(
            multipartAction!.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(SubmitPunchRequestDto));

        Assert.NotNull(jsonRequest.GetCustomAttribute<FromBodyAttribute>());
        Assert.NotNull(multipartRequest.GetCustomAttribute<FromFormAttribute>());
        Assert.NotNull(
            typeof(SubmitPunchRequestDto).GetProperty("ClientRequestId"));

        var consumes = multipartAction.GetCustomAttribute<ConsumesAttribute>();
        Assert.NotNull(consumes);
        Assert.Contains(
            "multipart/form-data",
            consumes!.ContentTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    private static OpenApiOperation ApplyFilter(string methodName)
    {
        var method = typeof(ContractProbeController).GetMethod(methodName);
        Assert.NotNull(method);

        var dataContractResolver = new JsonSerializerDataContractResolver(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            dataContractResolver);
        var schemaRepository = new SchemaRepository();
        var context = new OperationFilterContext(
            new ApiDescription(),
            schemaGenerator,
            schemaRepository,
            method!);
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses()
        };

        new ProblemDetailsResponseOperationFilter().Apply(
            operation,
            context);

        var schema = schemaRepository.Schemas[nameof(ApiProblemDetails)];
        Assert.Contains("traceId", schema.Properties.Keys);
        Assert.Contains("errorCode", schema.Properties.Keys);
        Assert.Contains("error", schema.Properties.Keys);
        Assert.Contains("errors", schema.Properties.Keys);

        return operation;
    }

    private static void AssertProblemResponses(
        OpenApiOperation operation,
        IEnumerable<string> statusCodes)
    {
        foreach (var statusCode in statusCodes)
        {
            Assert.True(
                operation.Responses.TryGetValue(
                    statusCode,
                    out var response),
                $"OpenAPI response {statusCode} is missing.");
            Assert.Contains(
                "application/problem+json",
                response!.Content.Keys);
        }
    }

    [Authorize]
    private sealed class ContractProbeController
    {
        public void Secured()
        {
        }

        [AllowAnonymous]
        public void Anonymous()
        {
        }
    }
}
