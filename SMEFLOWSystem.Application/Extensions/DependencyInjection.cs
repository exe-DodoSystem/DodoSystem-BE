using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Mappings;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Application.BackgroundJobs;

namespace SMEFLOWSystem.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(RoleMappingProfile).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IModuleSubscriptionService, ModuleSubscriptionService>();
        services.AddScoped<IBillingOrderModuleService, BillingOrderModuleService>();
        services.AddScoped<IBillingOrderService, BillingOrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<TenantExpirationRecurringJob>();

        return services;
    }
}
