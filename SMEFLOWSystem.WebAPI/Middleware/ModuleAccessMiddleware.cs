using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.SharedKernel.Interfaces;
using SMEFLOWSystem.WebAPI.Exceptions;
using System.Globalization;
using System.Text.Json;

namespace SMEFLOWSystem.WebAPI.Middleware;

public class ModuleAccessMiddleware
{
    private readonly RequestDelegate _next;

    private const int ModuleCacheSeconds = 3600;

    private sealed record ModuleCacheEntry(int Id);

    private static readonly (string Prefix, string ModuleCode)[] ProtectedPrefixes =
    {
        ("/api/hr", "HR"),

        ("/api/v1/attendance", "ATTENDANCE"),
        ("/api/v1/attendance/setting", "ATTENDANCE"),

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
            await WriteForbiddenAsync(context, "Thiếu TenantId");
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
            await WriteForbiddenAsync(
                context,
                $"Module '{required.ModuleCode}' chưa được cấu hình");
            return;
        }

        // Subscription state is security-sensitive. Read it directly so a SystemAdmin
        // suspension takes effect immediately instead of waiting for a cache entry to expire.
        var subscription = await moduleSubscriptionRepo
            .GetByTenantAndModuleIgnoreTenantAsync(tenantId.Value, moduleEntry.Id);
        if (subscription == null)
        {
            await WriteForbiddenAsync(
                context,
                $"Bạn chưa đăng ký module {required.ModuleCode}");
            return;
        }

        if (!ModuleSubscriptionRules.IsUsable(subscription, DateTime.UtcNow))
        {
            await WriteForbiddenAsync(
                context,
                $"Module {required.ModuleCode} đang tạm ngưng, chưa bắt đầu hoặc đã hết hạn");
            return;
        }

        await _next(context);
    }

    private static Task WriteForbiddenAsync(
        HttpContext context,
        string message)
    {
        return ApiProblemDetailsFactory.WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            message,
            "MODULE_ACCESS_FORBIDDEN",
            context.RequestAborted);
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
