using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.Exceptions;

namespace SMEFLOWSystem.WebAPI.Exceptions;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var mapping = MapException(httpContext, exception);
        var detail = mapping.Status == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;

        if (mapping.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled API exception. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                "Handled API exception {ExceptionType} ({ErrorCode}). TraceId: {TraceId}",
                exception.GetType().Name,
                mapping.ErrorCode,
                httpContext.TraceIdentifier);
        }

        await ApiProblemDetailsFactory.WriteAsync(
            httpContext,
            mapping.Status,
            mapping.Title,
            detail,
            mapping.ErrorCode,
            cancellationToken);

        return true;
    }

    private static ExceptionMapping MapException(
        HttpContext httpContext,
        Exception exception)
    {
        return exception switch
        {
            ConflictException conflict => new ExceptionMapping(
                StatusCodes.Status409Conflict,
                "Resource conflict",
                conflict.ErrorCode),
            BusinessRuleException business => new ExceptionMapping(
                StatusCodes.Status400BadRequest,
                "Business rule violation",
                business.ErrorCode),
            KeyNotFoundException => new ExceptionMapping(
                StatusCodes.Status404NotFound,
                "Resource not found",
                "RESOURCE_NOT_FOUND"),
            UnauthorizedAccessException
                when httpContext.User.Identity?.IsAuthenticated == true =>
                new ExceptionMapping(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "FORBIDDEN"),
            UnauthorizedAccessException => new ExceptionMapping(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "UNAUTHORIZED"),
            DownstreamServiceException downstream => new ExceptionMapping(
                downstream.ServiceUnavailable
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                downstream.ServiceUnavailable
                    ? "Downstream service unavailable"
                    : "Downstream service failure",
                downstream.ErrorCode),
            ArgumentException => new ExceptionMapping(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "VALIDATION_ERROR"),
            _ => new ExceptionMapping(
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "INTERNAL_SERVER_ERROR")
        };
    }

    private readonly record struct ExceptionMapping(
        int Status,
        string Title,
        string ErrorCode);
}
