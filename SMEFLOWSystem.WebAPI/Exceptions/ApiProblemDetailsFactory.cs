using Microsoft.AspNetCore.Mvc;

namespace SMEFLOWSystem.WebAPI.Exceptions;

public static class ApiProblemDetailsFactory
{
    public static ApiProblemDetails Create(
        HttpContext httpContext,
        int status,
        string title,
        string detail,
        string errorCode,
        IDictionary<string, string[]>? errors = null)
    {
        return new ApiProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path,
            TraceId = httpContext.TraceIdentifier,
            ErrorCode = errorCode,
            Error = detail,
            Errors = errors
        };
    }

    public static ObjectResult CreateResult(ApiProblemDetails problem)
    {
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var problem = Create(
            httpContext,
            status,
            title,
            detail,
            errorCode);
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
