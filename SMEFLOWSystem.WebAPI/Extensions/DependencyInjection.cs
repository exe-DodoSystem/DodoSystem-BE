using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SMEFLOWSystem.Core.Config;
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

            if (string.IsNullOrWhiteSpace(options.SendGridApiKey))
            {
                options.SendGridApiKey = configuration["EmailSettings:SendGridApiKey"]
                    ?? configuration["SendGrid:ApiKey"]
                    ?? configuration["EmailSettings:ApiKey"]
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

        _ = configuration["EmailSettings:SendGridApiKey"]
            ?? configuration["SendGrid:ApiKey"]
            ?? configuration["EmailSettings:ApiKey"]
            ?? throw new InvalidOperationException("Missing config: EmailSettings:SendGridApiKey (or SendGrid:ApiKey)");

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
