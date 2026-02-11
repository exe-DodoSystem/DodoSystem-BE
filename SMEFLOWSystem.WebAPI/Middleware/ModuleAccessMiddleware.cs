using Microsoft.AspNetCore.Http;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System.Globalization;

namespace SMEFLOWSystem.WebAPI.Middleware;

public class ModuleAccessMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly (string Prefix, string ModuleCode)[] ProtectedPrefixes =
    {
        ("/api/hr", "HR"),
        ("/api/employees", "HR"),

        ("/api/attendances", "ATTENDANCE"),
        ("/api/payrolls", "ATTENDANCE"),

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
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Thiếu TenantId");
            return;
        }

        var module = await moduleRepo.GetByCodeAsync(required.ModuleCode);
        if (module == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync($"Module '{required.ModuleCode}' chưa được cấu hình");
            return;
        }

        var sub = await moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId.Value, module.Id);
        if (sub == null || sub.IsDeleted)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync($"Bạn chưa đăng ký module {required.ModuleCode}");
            return;
        }

        var now = DateTime.UtcNow;
        var validStatus = string.Equals(sub.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(sub.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase);
        if (!validStatus || sub.EndDate < now)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync($"Module {required.ModuleCode} đã hết hạn");
            return;
        }

        await _next(context);
    }
}
