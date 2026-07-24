using FluentValidation;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Options;
using System;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemRevenueBreakdownQueryDtoValidator : AbstractValidator<SystemRevenueBreakdownQueryDto>
{
    public SystemRevenueBreakdownQueryDtoValidator(IOptions<SystemAnalyticsOptions> options)
    {
        Include(new SystemAnalyticsPeriodQueryDtoValidator(options));

        RuleFor(x => x.Dimension)
            .NotEmpty()
            .WithMessage("Chiều phân tích (Dimension) không được để trống.")
            .Must(x => string.Equals(x, SystemAnalyticsDimension.Module, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsDimension.Tenant, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsDimension.Gateway, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chiều phân tích (Dimension) phải là: 'module', 'tenant' hoặc 'gateway'.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(5, 50)
            .When(x => x.Limit.HasValue)
            .WithMessage("Giới hạn số phần tử (Limit) phải nằm trong khoảng từ 5 đến 50.");
    }
}
