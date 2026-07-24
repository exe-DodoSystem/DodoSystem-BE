using FluentValidation;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Options;
using System;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemRevenueForecastQueryDtoValidator : AbstractValidator<SystemRevenueForecastQueryDto>
{
    public SystemRevenueForecastQueryDtoValidator(IOptions<SystemAnalyticsOptions> options)
    {
        Include(new SystemAnalyticsPeriodQueryDtoValidator(options));

        RuleFor(x => x.ForecastPeriods)
            .InclusiveBetween(1, 6)
            .When(x => x.ForecastPeriods.HasValue)
            .WithMessage("Số kỳ dự báo (ForecastPeriods) phải từ 1 đến 6.");

        RuleFor(x => x.Granularity)
            .Must(x => string.IsNullOrWhiteSpace(x)
                       || string.Equals(x, SystemAnalyticsGranularity.Month, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Độ chia thời gian dự báo (Granularity) chỉ hỗ trợ 'month'.");
    }
}
