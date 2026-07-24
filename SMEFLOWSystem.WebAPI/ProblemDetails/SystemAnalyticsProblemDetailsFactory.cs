using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SMEFLOWSystem.WebAPI.ProblemDetails;

/// <summary>
/// Creates the RFC 7807 responses used only by the new System Analytics endpoints.
/// Existing controllers keep their current error behavior.
/// </summary>
public static class SystemAnalyticsProblemDetailsFactory
{
    public static ValidationProblemDetails Validation(
        HttpContext httpContext,
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };

        AddTraceId(problem, httpContext);
        return problem;
    }

    public static Microsoft.AspNetCore.Mvc.ProblemDetails NotFound(
        HttpContext httpContext,
        string detail)
    {
        return Create(
            httpContext,
            StatusCodes.Status404NotFound,
            "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            "The specified resource was not found.",
            detail);
    }

    public static Microsoft.AspNetCore.Mvc.ProblemDetails InsufficientForecastHistory(
        HttpContext httpContext,
        string detail)
    {
        return Create(
            httpContext,
            StatusCodes.Status422UnprocessableEntity,
            "https://tools.ietf.org/html/rfc4918#section-11.2",
            "Insufficient historical data for forecasting.",
            detail);
    }

    public static Microsoft.AspNetCore.Mvc.ProblemDetails UnexpectedError(HttpContext httpContext)
    {
        return Create(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            "An unexpected error occurred.",
            "The server could not complete the request.");
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string type,
        string title,
        string detail)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail
        };

        AddTraceId(problem, httpContext);
        return problem;
    }

    private static void AddTraceId(
        Microsoft.AspNetCore.Mvc.ProblemDetails problem,
        HttpContext httpContext)
    {
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }
}
