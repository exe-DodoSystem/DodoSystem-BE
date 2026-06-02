using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace SMEFLOWSystem.WebAPI.Middleware;

public class ModuleAccessMiddleware
{
    private readonly RequestDelegate _next;

    private const int SubscriptionCacheSeconds = 300;
    private const int ModuleCacheSeconds = 3600;

    private sealed record ModuleCacheEntry(int Id);
    private sealed record SubscriptionCacheEntry(string Status, DateTime EndDate);

    private static readonly (string Prefix, string ModuleCode)[] ProtectedPrefixes =
    {
        ("/api/hr", "HR"),

        ("/api/attendances", "ATTENDANCE"),
        ("/api/payrolls", "PAYROLL"),

        ("/api/customers", "SALES"),
        ("/api/orders", "SALES"),

        ("/api/tasks", "TASKS"),
        ("/api/projects", "TASKS"),

        ("/api/dashboard", "DASHBOARD"),
    };

    public ModuleAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantService currentTenantService,
        IDistributedCache cache,
        IModuleRepository moduleRepo,
        IModuleSubscriptionRepository moduleSubscriptionRepo)
    {
        var path = (context.Request.Path.Value ?? string.Empty).ToLowerInvariant();

        var required = ProtectedPrefixes.FirstOrDefault(p => path.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(required.Prefix))
        {
            await _next(context);
            return;
        }

        // Only enforce after authentication (Authorize will handle 401 if needed)
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tenantId = currentTenantService.TenantId;
        if (!tenantId.HasValue)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, "Thiếu TenantId");
            return;
        }

        var moduleCacheKey = $"module:code:{required.ModuleCode}";
        var moduleEntry = await GetFromDistributedCacheAsync<ModuleCacheEntry>(cache, moduleCacheKey);

        if (moduleEntry == null)
        {
            var m = await moduleRepo.GetByCodeAsync(required.ModuleCode);
            if (m != null)
            {
                moduleEntry = new ModuleCacheEntry(m.Id);
                await SetInDistributedCacheAsync(cache, moduleCacheKey, moduleEntry, TimeSpan.FromSeconds(ModuleCacheSeconds));
            }
        }

        if (moduleEntry == null)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, $"Module '{required.ModuleCode}' chưa được cấu hình");
            return;
        }

        var subCacheKey = $"moduleSub:tenant:{tenantId.Value}:module:{moduleEntry.Id}";
        var subEntry = await GetFromDistributedCacheAsync<SubscriptionCacheEntry>(cache, subCacheKey);

        if (subEntry == null)
        {
            var sub = await moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId.Value, moduleEntry.Id);
            if (sub != null)
            {
                subEntry = new SubscriptionCacheEntry(sub.Status ?? string.Empty, sub.EndDate);
                await SetInDistributedCacheAsync(cache, subCacheKey, subEntry, TimeSpan.FromSeconds(SubscriptionCacheSeconds));
            }
        }

        if (subEntry == null)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, $"Bạn chưa đăng ký module {required.ModuleCode}");
            return;
        }

        var now = DateTime.UtcNow;
        var validStatus = string.Equals(subEntry.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(subEntry.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase);
        if (!validStatus || subEntry.EndDate <= now)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, $"Module {required.ModuleCode} đã hết hạn");
            return;
        }

        await _next(context);
    }

    private static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(payload);
    }

    private static async Task<T?> GetFromDistributedCacheAsync<T>(IDistributedCache cache, string key)
    {
        var cachedString = await cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(cachedString)) return default;
        return JsonSerializer.Deserialize<T>(cachedString);
    }

    private static async Task SetInDistributedCacheAsync<T>(IDistributedCache cache, string key, T value, TimeSpan expiration)
    {
        var serialized = JsonSerializer.Serialize(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };
        await cache.SetStringAsync(key, serialized, options);
    }
}
