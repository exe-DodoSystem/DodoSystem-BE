using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using SMEFLOWSystem.WebAPI.Middleware;
using Hangfire;
using Hangfire.Common;
using SMEFLOWSystem.Application.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

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

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors("AllowFE");
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<ModuleAccessMiddleware>();

        InitializeDatabase(app);
        SeedRoles(app);
        SeedModules(app);

        // Schedule recurring jobs (daily at 00:00 Vietnam time)
        ScheduleRecurringJobs(app);

        app.MapControllers();

        return app;
    }

    private static void InitializeDatabase(WebApplication app)
    {
        const int maxRetries = 12;
        var delay = TimeSpan.FromSeconds(5);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SMEFLOWSystemContext>();
                db.Database.Migrate();
                return;
            }
            catch when (attempt < maxRetries)
            {
                Thread.Sleep(delay);
            }
        }

        using var finalScope = app.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<SMEFLOWSystemContext>();
        finalDb.Database.Migrate();
    }

    private static void SeedRoles(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SMEFLOWSystemContext>();

        SeedRoleIfMissing(db, "TenantAdmin", "Tenant Admin");
        SeedRoleIfMissing(db, "Manager", "Manager");
        SeedRoleIfMissing(db, "HRManager", "HR Manager");
        SeedRoleIfMissing(db, "SystemAdmin", "System Admin");

        db.SaveChanges();
    }

    private static void SeedModules(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SMEFLOWSystemContext>();
        SeedModulesIfMissing(db, "HR", "HR", "Human Resource Management", "Module quản lý nhân sự", 150000m, true);
        SeedModulesIfMissing(db, "ATTENDANCE", "ATT", "Attendance Management", "Module quản lý chấm công", 180000m, true);
        SeedModulesIfMissing(db, "PAYROLL", "PAYROLL", "Payroll Management", "Module quản lý bảng lương", 180000m, true);
        SeedModulesIfMissing(db, "DASHBOARD", "DASH", "Dashboard Management", "Module quản lý Dashboard & Báo cáo", 120000m, true);
        db.SaveChanges();
    }

    private static void SeedRoleIfMissing(SMEFLOWSystemContext db, string roleName, string description)
    {
        var exists = db.Roles.AsNoTracking().Any(r => r.Name == roleName);
        if (exists) return;

        db.Roles.Add(new Role
        {
            Name = roleName,
            Description = description,
            IsSystemRole = true
        });
    }

    private static void SeedModulesIfMissing(SMEFLOWSystemContext db, string moduleCode, string shortCode, string moduleName, string description, decimal monthlyPrice, bool isActive)
    {
        var exists = db.Modules.AsNoTracking().Any(m => m.Code == moduleCode);

        if(exists) return;

        db.Modules.Add(new Module
        {
            Code = moduleCode,
            ShortCode = shortCode,
            Name = moduleName,
            Description = description,
            MonthlyPrice = monthlyPrice,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void ScheduleRecurringJobs(WebApplication app)
    {
        var timeZone = TryGetVietNamTimeZone();
        var attendanceEnabled = app.Configuration.GetValue<bool?>("AttendanceResolution:Enabled") ?? true;
        var attendanceCron = app.Configuration["AttendanceResolution:Cron"] ?? "*/15 * * * *";
        using var scope = app.Services.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate(
            recurringJobId: "tenant-expiration",
            job: Job.FromExpression<TenantExpirationRecurringJob>(j => j.SuspendExpiredTenantsAndSendRenewalEmailsAsync()),
            cronExpression: "0 0 * * *",
            options: new RecurringJobOptions { TimeZone = timeZone });

        recurringJobManager.AddOrUpdate(
            recurringJobId: "monthly-payroll",
            job: Job.FromExpression<PayrollRecurringJob>(j => j.GeneratePayrollForAllTenant()),
            cronExpression: "0 1 1 * *",   // 01:00 AM ngày 1 hàng tháng (giờ VN)
            options: new RecurringJobOptions { TimeZone = timeZone });

        if (attendanceEnabled)
        {
            recurringJobManager.AddOrUpdate(
                recurringJobId: "attendance-resolution",
                job: Job.FromExpression<AttendanceResolutionRecurringJob>(j => j.RunAsync()),
                cronExpression: attendanceCron,
                options: new RecurringJobOptions { TimeZone = timeZone });
        }

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
