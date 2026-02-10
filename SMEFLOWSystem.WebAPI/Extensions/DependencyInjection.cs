using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SMEFLOWSystem.Core.Config;
using System.Text;

namespace SMEFLOWSystem.WebAPI.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        ValidateConfiguration(configuration);

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

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
            cfg.UseSqlServerStorage(
                configuration.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions());
        });
        services.AddHangfireServer();

        var jwtSecret = GetRequiredConfig(configuration, "Jwt:Secret");
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
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
            _ = GetRequiredConfig(configuration, "Payment:VNPay:ReturnUrl");
            _ = GetRequiredConfig(configuration, "Payment:VNPay:HashSecret");
            _ = GetRequiredConfig(configuration, "Payment:VNPay:PaymentUrl");
        }
    }

    private static string GetRequiredConfig(IConfiguration config, string key)
    {
        return config[key] ?? throw new InvalidOperationException($"Missing config: {key}");
    }
}
