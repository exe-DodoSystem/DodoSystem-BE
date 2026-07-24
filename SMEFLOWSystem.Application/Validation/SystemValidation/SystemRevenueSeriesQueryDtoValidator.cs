using FluentValidation;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Options;
using System;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemRevenueSeriesQueryDtoValidator : AbstractValidator<SystemRevenueSeriesQueryDto>
{
    public SystemRevenueSeriesQueryDtoValidator(IOptions<SystemAnalyticsOptions> options)
    {
        Include(new SystemAnalyticsPeriodQueryDtoValidator(options));

        RuleFor(x => x.Granularity)
            .Must(x => string.IsNullOrWhiteSpace(x)
                       || string.Equals(x, SystemAnalyticsGranularity.Day, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsGranularity.Week, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsGranularity.Month, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Độ chia thời gian (Granularity) phải là: 'day', 'week' hoặc 'month'.");
    }
}
