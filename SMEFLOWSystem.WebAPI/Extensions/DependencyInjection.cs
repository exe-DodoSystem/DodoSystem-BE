using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Core.Config;
using SMEFLOWSystem.WebAPI.BackgroundServices;
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
        services.AddControllers();
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
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization();

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
        services.Configure<FacePlusPlusSettings>(configuration.GetSection("FacePlusPlus"));
        services.AddHttpClient("FacePlusPlus");
        services.PostConfigure<EmailSettings>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.FromName))
            {
                options.FromName = configuration["EmailSettings:FromName"]
                    ?? configuration["EmailSettings:SenderName"]
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.FromEmail))
            {
                options.FromEmail = configuration["EmailSettings:FromEmail"]
                    ?? configuration["EmailSettings:SenderEmail"]
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.SmtpHost))
            {
                options.SmtpHost = configuration["EmailSettings:SmtpHost"]
                    ?? string.Empty;
            }

            if (options.SmtpPort <= 0)
            {
                var smtpPortConfig = configuration["EmailSettings:SmtpPort"];
                options.SmtpPort = int.TryParse(smtpPortConfig, out var smtpPort) ? smtpPort : 587;
            }

            if (string.IsNullOrWhiteSpace(options.SmtpUsername))
            {
                options.SmtpUsername = configuration["EmailSettings:SmtpUsername"]
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.SmtpPassword))
            {
                options.SmtpPassword = configuration["EmailSettings:SmtpPassword"]
                    ?? string.Empty;
            }
        });

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

        _ = configuration["EmailSettings:FromName"] ?? configuration["EmailSettings:SenderName"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:FromName (or legacy EmailSettings:SenderName)");
        _ = configuration["EmailSettings:FromEmail"] ?? configuration["EmailSettings:SenderEmail"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:FromEmail (or legacy EmailSettings:SenderEmail)");

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
