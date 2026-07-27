using Microsoft.AspNetCore.Http;
using SMEFLOWSystem.WebAPI.Exceptions;

namespace SMEFLOWSystem.WebAPI.ProblemDetails;

/// <summary>
/// Creates the canonical RFC 7807 responses used by System Analytics endpoints.
/// </summary>
public static class SystemAnalyticsProblemDetailsFactory
{
    public static ApiProblemDetails Validation(
        HttpContext httpContext,
        IDictionary<string, string[]> errors)
    {
        return ApiProblemDetailsFactory.Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation failed",
            "One or more validation errors occurred.",
            "SYSTEM_ANALYTICS_VALIDATION_ERROR",
            errors);
    }

    public static ApiProblemDetails NotFound(
        HttpContext httpContext,
        string detail)
    {
        return ApiProblemDetailsFactory.Create(
            httpContext,
            StatusCodes.Status404NotFound,
            "The specified resource was not found.",
            detail,
            "SYSTEM_ANALYTICS_RESOURCE_NOT_FOUND");
    }

    public static ApiProblemDetails InsufficientForecastHistory(
        HttpContext httpContext,
        string detail)
    {
        return ApiProblemDetailsFactory.Create(
            httpContext,
            StatusCodes.Status422UnprocessableEntity,
            "Insufficient historical data for forecasting.",
            detail,
            "INSUFFICIENT_FORECAST_HISTORY");
    }

    public static ApiProblemDetails UnexpectedError(HttpContext httpContext)
    {
        return ApiProblemDetailsFactory.Create(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "Internal server error",
            "The server could not complete the request.",
            "INTERNAL_SERVER_ERROR");
    }
}
