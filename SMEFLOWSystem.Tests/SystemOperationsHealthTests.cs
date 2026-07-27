using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SMEFLOWSystem.WebAPI.Services;

namespace SMEFLOWSystem.Tests;

public sealed class SystemOperationsHealthTests
{
    [Fact]
    public async Task HealthyDependencies_ReturnSafeCanonicalSummaryAndUseCache()
    {
        var postgres = new StubHealthCheck(HealthCheckResult.Healthy(
            "Host=db.internal;Username=admin;Password=secret"));
        var redis = new StubHealthCheck(HealthCheckResult.Healthy(
            "redis://cache.internal:6379"));
        var rabbitMq = new StubHealthCheck(HealthCheckResult.Healthy(
            "amqp://guest:guest@rabbit.internal"));
        using var provider = BuildProvider(postgres, redis, rabbitMq);
        var service = new SystemOperationsHealthService(
            provider.GetRequiredService<HealthCheckService>(),
            new MemoryCache(new MemoryCacheOptions()));

        var first = await service.GetHealthSummaryAsync();
        var second = await service.GetHealthSummaryAsync();
        var json = JsonSerializer.Serialize(first);

        Assert.Same(first, second);
        Assert.Equal("Healthy", first.Status);
        Assert.Equal(DateTimeKind.Utc, first.CheckedAt.Kind);
        Assert.Equal(["postgres", "rabbitmq", "redis"],
            first.Components.Select(component => component.Name).ToArray());
        Assert.All(first.Components, component =>
        {
            Assert.Equal("Healthy", component.Status);
            Assert.True(component.DurationMs >= 0);
            Assert.NotNull(component.Description);
        });
        Assert.DoesNotContain("db.internal", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cache.internal", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rabbit.internal", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guest", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, postgres.InvocationCount);
        Assert.Equal(1, redis.InvocationCount);
        Assert.Equal(1, rabbitMq.InvocationCount);
    }

    [Fact]
    public async Task UnhealthyDependency_ControlsOverallStatusWithoutLeakingException()
    {
        var postgres = new StubHealthCheck(HealthCheckResult.Healthy());
        var redis = new StubHealthCheck(HealthCheckResult.Degraded(
            "Host=redis.internal;Password=redis-secret",
            new InvalidOperationException("Stack trace redis-secret")));
        var rabbitMq = new StubHealthCheck(HealthCheckResult.Unhealthy(
            "amqp://admin:rabbit-secret@rabbit.internal",
            new InvalidOperationException("ConnectionString=rabbit-secret")));
        using var provider = BuildProvider(postgres, redis, rabbitMq);
        var service = new SystemOperationsHealthService(
            provider.GetRequiredService<HealthCheckService>(),
            new MemoryCache(new MemoryCacheOptions()));

        var result = await service.GetHealthSummaryAsync();
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("Unhealthy", result.Status);
        Assert.Equal(
            "Degraded",
            result.Components.Single(component => component.Name == "redis").Status);
        Assert.Equal(
            "Unhealthy",
            result.Components.Single(component => component.Name == "rabbitmq").Status);
        Assert.Equal(
            "RabbitMQ message broker is not reachable.",
            result.Components.Single(component => component.Name == "rabbitmq")
                .Description);
        Assert.DoesNotContain("internal", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack trace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_IsPropagatedToHealthCheckPipeline()
    {
        using var provider = BuildProvider(
            new StubHealthCheck(HealthCheckResult.Healthy()),
            new StubHealthCheck(HealthCheckResult.Healthy()),
            new StubHealthCheck(HealthCheckResult.Healthy()));
        var service = new SystemOperationsHealthService(
            provider.GetRequiredService<HealthCheckService>(),
            new MemoryCache(new MemoryCacheOptions()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetHealthSummaryAsync(cancellation.Token));
    }

    private static ServiceProvider BuildProvider(
        StubHealthCheck postgres,
        StubHealthCheck redis,
        StubHealthCheck rabbitMq)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck("postgres", postgres)
            .AddCheck("redis", redis)
            .AddCheck("rabbitmq", rabbitMq);
        return services.BuildServiceProvider();
    }

    private sealed class StubHealthCheck : IHealthCheck
    {
        private readonly HealthCheckResult _result;

        public StubHealthCheck(HealthCheckResult result)
        {
            _result = result;
        }

        public int InvocationCount { get; private set; }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(_result);
        }
    }
}
