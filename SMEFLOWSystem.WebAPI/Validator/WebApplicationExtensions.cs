using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using SMEFLOWSystem.WebAPI.Middleware;
using Hangfire;
using Hangfire.Common;
using SMEFLOWSystem.Application.BackgroundJobs;

namespace SMEFLOWSystem.WebAPI.Validator;

public static class WebApplicationExtensions
{
    public static WebApplication UseWebApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<ModuleAccessMiddleware>();

        // Schedule recurring jobs (daily at 00:00 Vietnam time)
        ScheduleRecurringJobs(app);

        app.MapControllers();

        return app;
    }

    private static void ScheduleRecurringJobs(WebApplication app)
    {
        var timeZone = TryGetVietNamTimeZone();
        using var scope = app.Services.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate(
            recurringJobId: "tenant-expiration",
            job: Job.FromExpression<TenantExpirationRecurringJob>(j => j.SuspendExpiredTenantsAndSendRenewalEmailsAsync()),
            cronExpression: "0 0 * * *",
            options: new RecurringJobOptions { TimeZone = timeZone });
    }

    private static TimeZoneInfo TryGetVietNamTimeZone()
    {
        // Windows: SE Asia Standard Time (UTC+7)
        // Linux: Asia/Ho_Chi_Minh
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { /* ignore */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { /* ignore */ }
        return TimeZoneInfo.Utc;
    }
}
