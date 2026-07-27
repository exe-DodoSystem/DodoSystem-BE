using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.WebAPI.Services;

public interface ISystemOperationsHealthService
{
    Task<SystemOperationsHealthResponseDto> GetHealthSummaryAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SystemOperationsHealthService :
    ISystemOperationsHealthService
{
    private const string CacheKey = "system-operations:health-summary:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly HealthCheckService _healthCheckService;
    private readonly IMemoryCache _cache;

    public SystemOperationsHealthService(
        HealthCheckService healthCheckService,
        IMemoryCache cache)
    {
        _healthCheckService = healthCheckService;
        _cache = cache;
    }

    public async Task<SystemOperationsHealthResponseDto> GetHealthSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
                CacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                    var report = await _healthCheckService.CheckHealthAsync(
                        cancellationToken);
                    return MapReport(report);
                })
            ?? throw new InvalidOperationException(
                "Health summary cache factory returned no value.");
    }

    internal static SystemOperationsHealthResponseDto MapReport(
        HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new SystemOperationsHealthResponseDto
        {
            Status = MapStatus(report.Status),
            CheckedAt = DateTime.UtcNow,
            DurationMs = ToDurationMilliseconds(report.TotalDuration),
            Components = report.Entries
                .Select(entry => new SystemOperationsHealthComponentDto
                {
                    Name = SanitizeComponentName(entry.Key),
                    Status = MapStatus(entry.Value.Status),
                    DurationMs = ToDurationMilliseconds(entry.Value.Duration),
                    Description = BuildSafeDescription(
                        entry.Key,
                        entry.Value.Status)
                })
                .OrderBy(component => component.Name, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static string MapStatus(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "Healthy",
            HealthStatus.Degraded => "Degraded",
            HealthStatus.Unhealthy => "Unhealthy",
            _ => "Unhealthy"
        };
    }

    private static long ToDurationMilliseconds(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return (long)Math.Ceiling(duration.TotalMilliseconds);
    }

    private static string SanitizeComponentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "dependency";
        }

        var sanitized = new string(name
            .Where(character =>
                char.IsLetterOrDigit(character)
                || character is '-' or '_')
            .Take(64)
            .ToArray())
            .ToLowerInvariant();
        return sanitized.Length == 0 ? "dependency" : sanitized;
    }

    private static string BuildSafeDescription(
        string componentName,
        HealthStatus status)
    {
        var component = componentName.Trim().ToLowerInvariant() switch
        {
            "postgres" => "PostgreSQL database",
            "redis" => "Redis cache store",
            "rabbitmq" => "RabbitMQ message broker",
            _ => "Dependency"
        };
        var state = status switch
        {
            HealthStatus.Healthy => "is healthy.",
            HealthStatus.Degraded => "is degraded.",
            HealthStatus.Unhealthy => "is not reachable.",
            _ => "status is unavailable."
        };
        return $"{component} {state}";
    }
}
