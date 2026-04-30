using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.Abstractions.Messaging;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Messaging.Consumers;
using SMEFLOWSystem.Infrastructure.Messaging.RabbitMQ;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.Infrastructure.Services;
using SMEFLOWSystem.Infrastructure.Tenancy;
using SMEFLOWSystem.Infrastructure.Options;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

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
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IShiftPatternRepository, ShiftPatternRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IDailyTimesheetRepository, DailyTimesheetRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddScoped<IRawPunchLogRepository, RawPunchLogRepository>();
        // services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAttendanceSettingRepository, AttendanceSettingRepository>();
        services.AddScoped<IOvertimeRequestRepository, OvertimeRequestRepository>();
        services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<ITimesheetAppealRepository, TimesheetAppealRepository>();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITransaction, Transaction>();

        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFaceVerificationService, FacePlusPlusVerificationService>();
        services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
        services.AddScoped<IRabbitMessageHandler, PaymentSucceededConsumer>();
        services.AddScoped<IRabbitMessageHandler, EmailSendConsumer>();

        return services;
    }
}
