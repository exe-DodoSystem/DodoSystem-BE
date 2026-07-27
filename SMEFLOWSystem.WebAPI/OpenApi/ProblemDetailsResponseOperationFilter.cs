using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.WebAPI.Exceptions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SMEFLOWSystem.WebAPI.OpenApi;

public sealed class ProblemDetailsResponseOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, string>
        CommonResponses = new Dictionary<string, string>
        {
            ["400"] = "Validation or business rule violation",
            ["404"] = "Resource not found",
            ["409"] = "Resource state conflict",
            ["500"] = "Unexpected server error",
            ["502"] = "Downstream service failure",
            ["503"] = "Downstream service unavailable"
        };

    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var problemSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ApiProblemDetails),
            context.SchemaRepository);

        foreach (var response in CommonResponses)
        {
            AddProblemResponse(
                operation,
                response.Key,
                response.Value,
                problemSchema);
        }

        if (RequiresAuthorization(context.MethodInfo))
        {
            AddProblemResponse(
                operation,
                "401",
                "Authentication is required",
                problemSchema);
            AddProblemResponse(
                operation,
                "403",
                "Authenticated caller does not have access",
                problemSchema);
        }
    }

    private static bool RequiresAuthorization(
        System.Reflection.MethodInfo method)
    {
        var controller = method.DeclaringType;
        var allowsAnonymous =
            method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) ||
            controller?.IsDefined(
                typeof(AllowAnonymousAttribute),
                inherit: true) == true;
        if (allowsAnonymous)
        {
            return false;
        }

        return method
                .GetCustomAttributes(inherit: true)
                .OfType<IAuthorizeData>()
                .Any() ||
            controller?
                .GetCustomAttributes(inherit: true)
                .OfType<IAuthorizeData>()
                .Any() == true;
    }

    private static void AddProblemResponse(
        OpenApiOperation operation,
        string statusCode,
        string description,
        OpenApiSchema schema)
    {
        if (!operation.Responses.TryGetValue(
                statusCode,
                out var response))
        {
            response = new OpenApiResponse
            {
                Description = description
            };
            operation.Responses[statusCode] = response;
        }

        response.Content.TryAdd(
            "application/problem+json",
            new OpenApiMediaType
            {
                Schema = schema
            });
    }
}
