using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
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
        IMemoryCache cache,
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
        var moduleEntry = await cache.GetOrCreateAsync(moduleCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ModuleCacheSeconds);
            var m = await moduleRepo.GetByCodeAsync(required.ModuleCode);
            return m == null ? null : new ModuleCacheEntry(m.Id);
        });

        if (moduleEntry == null)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, $"Module '{required.ModuleCode}' chưa được cấu hình");
            return;
        }

        var subCacheKey = $"moduleSub:tenant:{tenantId.Value}:module:{moduleEntry.Id}";
        var subEntry = await cache.GetOrCreateAsync(subCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(SubscriptionCacheSeconds);
            var sub = await moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId.Value, moduleEntry.Id);
            if (sub == null) return null;
            return new SubscriptionCacheEntry(sub.Status ?? string.Empty, sub.EndDate);
        });

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
}
