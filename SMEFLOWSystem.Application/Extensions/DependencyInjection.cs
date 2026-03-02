using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Mappings;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Application.Services.System;
using SMEFLOWSystem.Application.BackgroundJobs;
using SMEFLOWSystem.Application.Validation.AuthValidation;
using SMEFLOWSystem.Application.Validation.HRValidation;
using Microsoft.Extensions.Configuration;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using VNPAY.NET;

namespace SMEFLOWSystem.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(RoleMappingProfile).Assembly);

        services.AddValidatorsFromAssemblyContaining<RegisterRequestDtoValidator>();
        services.AddValidatorsFromAssemblyContaining<ChangePasswordRequestDtoValidator>();
        services.AddValidatorsFromAssemblyContaining<ForgotPasswordRequestDtoValidator>();
        services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();
        services.AddValidatorsFromAssemblyContaining<ResetPasswordWithOtpDtoValidator>();

        services.AddValidatorsFromAssemblyContaining<DepartmentCreateDtoValidator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IModuleSubscriptionService, ModuleSubscriptionService>();
        services.AddScoped<IBillingOrderModuleService, BillingOrderModuleService>();
        services.AddScoped<IBillingOrderService, BillingOrderService>();
        services.AddSingleton<IVnpay, Vnpay>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<TenantExpirationRecurringJob>();
        services.AddScoped<IOTPService, OTPService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<IHrDepartmentService, HrDepartmentService>();
        services.AddScoped<IHrPositionService, HrPositionService>();
        services.AddScoped<IHrEmployeeService, HrEmployeeService>();

        services.AddScoped<ISystemBootstrapService, SystemBootstrapService>();
        services.AddScoped<ISystemTenantService, SystemTenantService>();

        return services;
    }
}
