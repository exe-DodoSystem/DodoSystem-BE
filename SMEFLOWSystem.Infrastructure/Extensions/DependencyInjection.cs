using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.Infrastructure.Tenancy;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SMEFLOWSystemContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IModuleSubscriptionRepository, ModuleSubscriptionRepository>();
        services.AddScoped<IBillingOrderModuleRepository, BillingOrderModuleRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IBillingOrderRepository, BillingOrderRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ITransaction, Transaction>();

        return services;
    }
}
