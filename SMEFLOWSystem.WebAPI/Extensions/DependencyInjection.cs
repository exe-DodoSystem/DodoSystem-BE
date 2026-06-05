using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Core.Config;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.BackgroundServices;
using SMEFLOWSystem.WebAPI.Converters;
using SMEFLOWSystem.WebAPI.Hubs;
using SMEFLOWSystem.WebAPI.Services;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace SMEFLOWSystem.WebAPI.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        ValidateConfiguration(configuration);
        services.AddDistributedMemoryCache();
        services.AddMemoryCache();
        services.AddHostedService<OutboxPublisherHostedService>();
        services.AddHostedService<RabbitMqSubscriberHostedService>();
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new TimeSpanJsonConverter());
            });
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SMEFLOWSystem API",
                Version = "v1"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Thêm cấu hình đọc file XML comments
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.TenantAdmin, policy =>
                policy.RequireRole(RoleConstants.TenantAdmin));

            options.AddPolicy(PolicyNames.HrManager, policy =>
                policy.RequireRole(RoleConstants.HrManager));

            options.AddPolicy(PolicyNames.Manager, policy =>
                policy.RequireRole(RoleConstants.Manager));

            options.AddPolicy(PolicyNames.Employee, policy =>
                policy.RequireRole(RoleConstants.Employee));

            options.AddPolicy(PolicyNames.SystemAdmin, policy =>
                policy.RequireRole(RoleConstants.SystemAdmin));

            // Composite policies
            options.AddPolicy(PolicyNames.HrAccess, policy =>
                policy.RequireRole(
                    RoleConstants.TenantAdmin,
                    RoleConstants.HrManager,
                    RoleConstants.Manager));

            options.AddPolicy(PolicyNames.AdminOrHr, policy =>
                policy.RequireRole(
                    RoleConstants.TenantAdmin, 
                    RoleConstants.HrManager));
        });

        services.Configure<AttendanceResolutionOptions>(configuration.GetSection("AttendanceResolution"));

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
        services.Configure<FacePlusPlusSettings>(configuration.GetSection("FacePlusPlus"));
        services.AddHttpClient("FacePlusPlus");

        services.AddHangfire(cfg =>
        {
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
            cfg.UseSimpleAssemblyNameTypeSerializer();
            cfg.UseRecommendedSerializerSettings();

            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                throw new InvalidOperationException("Missing config: ConnectionStrings:Redis");
            }

            cfg.UseRedisStorage(redisConnectionString);
        });
        services.AddHangfireServer();

        var jwtSecret = GetRequiredConfig(configuration, "Jwt:Secret");
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),

                    // Token đang phát roles theo ClaimTypes.Role trong AuthHelper
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        // SignalR
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, UserIdProvider>();
        services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFE", policy =>
            {
                var allowedOriginsConfig = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
                var allowedOrigins = new List<string>();
                if (allowedOriginsConfig != null && allowedOriginsConfig.Length > 0)
                {
                    allowedOrigins.AddRange(allowedOriginsConfig);
                }
                else
                {
                    allowedOrigins.Add("http://localhost:3000");
                    allowedOrigins.Add("http://localhost:5173");
                    var frontendUrl = configuration["Payment:FrontendUrl"];
                    if (!string.IsNullOrWhiteSpace(frontendUrl))
                    {
                        allowedOrigins.Add(frontendUrl.TrimEnd('/'));
                    }
                }

                policy.WithOrigins(allowedOrigins.Distinct().ToArray())
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });
        return services;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        _ = GetRequiredConfig(configuration, "Jwt:Secret");
        _ = GetRequiredConfig(configuration, "Jwt:Issuer");
        _ = GetRequiredConfig(configuration, "Jwt:Audience");

        _ = configuration["EmailSettings:SmtpHost"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:SmtpHost");
        _ = configuration["EmailSettings:SmtpPort"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:SmtpPort");
        _ = configuration["EmailSettings:SmtpUsername"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:SmtpUsername");
        _ = configuration["EmailSettings:SmtpPassword"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:SmtpPassword");

        _ = configuration["EmailSettings:FromName"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:FromName");
        _ = configuration["EmailSettings:FromEmail"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:FromEmail");

        var paymentMode = GetRequiredConfig(configuration, "Payment:Mode");
        var paymentGateway = GetRequiredConfig(configuration, "Payment:Gateway");
        if ((paymentMode == "Sandbox" || paymentMode == "Production") && paymentGateway == "VNPay")
        {
            _ = GetRequiredConfig(configuration, "Payment:VNPay:TmnCode");
            _ = GetRequiredConfig(configuration, "Payment:VNPay:CallbackUrl");
            _ = GetRequiredConfig(configuration, "Payment:VNPay:HashSecret");
            _ = GetRequiredConfig(configuration, "Payment:VNPay:BaseUrl");
        }
    }

    private static string GetRequiredConfig(IConfiguration config, string key)
    {
        return config[key] ?? throw new InvalidOperationException($"Missing config: {key}");
    }
}
