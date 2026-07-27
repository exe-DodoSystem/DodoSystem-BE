using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.WebAPI.Exceptions;

namespace SMEFLOWSystem.Tests;

public sealed class ApiExceptionContractTests
{
    public static IEnumerable<object[]> ExceptionMappings()
    {
        yield return
        [
            new BusinessRuleException(
                "Business validation failed.",
                "BUSINESS_TEST"),
            false,
            StatusCodes.Status400BadRequest,
            "BUSINESS_TEST",
            LogLevel.Warning
        ];
        yield return
        [
            new ArgumentException("Input is invalid."),
            false,
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            LogLevel.Warning
        ];
        yield return
        [
            new KeyNotFoundException("Resource was not found."),
            false,
            StatusCodes.Status404NotFound,
            "RESOURCE_NOT_FOUND",
            LogLevel.Warning
        ];
        yield return
        [
            new ConflictException("Resource is no longer editable.", "STATE_CONFLICT"),
            false,
            StatusCodes.Status409Conflict,
            "STATE_CONFLICT",
            LogLevel.Warning
        ];
        yield return
        [
            new UnauthorizedAccessException("Authentication is required."),
            false,
            StatusCodes.Status401Unauthorized,
            "UNAUTHORIZED",
            LogLevel.Warning
        ];
        yield return
        [
            new UnauthorizedAccessException("Caller is outside this scope."),
            true,
            StatusCodes.Status403Forbidden,
            "FORBIDDEN",
            LogLevel.Warning
        ];
        yield return
        [
            new DownstreamServiceException(
                "Provider returned an invalid response.",
                "PAYMENT_PROVIDER_FAILURE"),
            false,
            StatusCodes.Status502BadGateway,
            "PAYMENT_PROVIDER_FAILURE",
            LogLevel.Error
        ];
        yield return
        [
            new DownstreamServiceException(
                "Provider is temporarily unavailable.",
                "PAYMENT_PROVIDER_UNAVAILABLE",
                serviceUnavailable: true),
            false,
            StatusCodes.Status503ServiceUnavailable,
            "PAYMENT_PROVIDER_UNAVAILABLE",
            LogLevel.Error
        ];
    }

    [Theory]
    [MemberData(nameof(ExceptionMappings))]
    [Trait("Phase", "7")]
    [Trait("Gap", "BE-MGR-06")]
    public async Task TypedException_ReturnsExpectedProblemDetails(
        Exception exception,
        bool authenticated,
        int expectedStatus,
        string expectedErrorCode,
        LogLevel expectedLogLevel)
    {
        const string traceId = "phase-7-trace";
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var context = CreateContext(authenticated, traceId);
        var handler = new ApiExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType,
            StringComparison.OrdinalIgnoreCase);

        using var problem = await ReadProblemAsync(context);
        Assert.Equal(
            expectedStatus,
            problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            traceId,
            problem.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(
            expectedErrorCode,
            problem.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(
            exception.Message,
            problem.RootElement.GetProperty("error").GetString());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(expectedLogLevel, entry.Level);
        Assert.Equal(
            expectedStatus >= StatusCodes.Status500InternalServerError
                ? exception
                : null,
            entry.Exception);
    }

    [Fact]
    [Trait("Phase", "7")]
    [Trait("Gap", "BE-MGR-06")]
    public async Task UnknownException_ReturnsSafe500AndLogsOnce()
    {
        const string secret = "database-password=do-not-leak";
        const string traceId = "phase-7-unknown-trace";
        var exception = new InvalidOperationException(secret);
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var context = CreateContext(authenticated: true, traceId);
        var handler = new ApiExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            nameof(InvalidOperationException),
            body,
            StringComparison.Ordinal);

        using var problem = JsonDocument.Parse(body);
        Assert.Equal(
            "An unexpected error occurred.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(
            "An unexpected error occurred.",
            problem.RootElement.GetProperty("error").GetString());
        Assert.Equal(
            "INTERNAL_SERVER_ERROR",
            problem.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(
            traceId,
            problem.RootElement.GetProperty("traceId").GetString());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    [Trait("Phase", "7")]
    [Trait("Phase", "8")]
    public async Task ProblemDetailsFactory_WritesAuthorizationContract()
    {
        const string traceId = "authorization-contract-trace";
        var context = CreateContext(authenticated: true, traceId);

        await ApiProblemDetailsFactory.WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "The caller is outside the permitted scope.",
            "FORBIDDEN");

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            context.Response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType,
            StringComparison.OrdinalIgnoreCase);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal(
            traceId,
            problem.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(
            "FORBIDDEN",
            problem.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(
            "The caller is outside the permitted scope.",
            problem.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    [Trait("Phase", "7")]
    [Trait("Phase", "8")]
    public void ProblemDetailsFactory_PreservesValidationErrors()
    {
        var context = CreateContext(
            authenticated: false,
            "validation-contract-trace");
        var errors = new Dictionary<string, string[]>
        {
            ["clientRequestId"] = ["The value is invalid."]
        };

        var problem = ApiProblemDetailsFactory.Create(
            context,
            StatusCodes.Status400BadRequest,
            "Validation failed",
            "One or more validation errors occurred.",
            "VALIDATION_ERROR",
            errors);

        Assert.Equal("VALIDATION_ERROR", problem.ErrorCode);
        Assert.Same(errors, problem.Errors);
        var problemErrors = Assert.IsAssignableFrom<
            IDictionary<string, string[]>>(problem.Errors);
        Assert.Equal(
            "The value is invalid.",
            Assert.Single(problemErrors["clientRequestId"]));
    }

    private static DefaultHttpContext CreateContext(
        bool authenticated,
        string traceId)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = traceId
        };
        context.Request.Path = "/api/phase-7";
        context.Response.Body = new MemoryStream();

        if (authenticated)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                    authenticationType: "Phase7Test"));
        }

        return context;
    }

    private static async Task<JsonDocument> ReadProblemAsync(
        HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        Exception? Exception);
}
