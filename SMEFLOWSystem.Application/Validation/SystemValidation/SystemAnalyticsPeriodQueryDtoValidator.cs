using FluentValidation;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Options;
using System;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemAnalyticsPeriodQueryDtoValidator : AbstractValidator<SystemAnalyticsPeriodQueryDto>
{
    public SystemAnalyticsPeriodQueryDtoValidator(IOptions<SystemAnalyticsOptions> options)
    {
        RuleFor(x => x)
            .Custom((query, context) =>
            {
                if (!string.Equals(
                        query.Timezone,
                        SystemAnalyticsOptions.SupportedTimezone,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    AnalyticsPeriodResolver.Resolve(query, options.Value);
                }
                catch (AnalyticsPeriodValidationException exception)
                {
                    context.AddFailure(exception.PropertyName, exception.Message);
                }
            });

        RuleFor(x => x.Timezone)
            .Equal(SystemAnalyticsOptions.SupportedTimezone, StringComparer.OrdinalIgnoreCase)
            .WithMessage("Múi giờ chỉ hỗ trợ 'Asia/Ho_Chi_Minh'.");

        RuleFor(x => x.Currency)
            .Equal(SystemAnalyticsOptions.SupportedCurrency, StringComparer.OrdinalIgnoreCase)
            .WithMessage("Tiền tệ chỉ hỗ trợ 'VND'.");

        RuleFor(x => x.Compare)
            .Must(x => string.Equals(x, SystemAnalyticsCompare.PreviousPeriod, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsCompare.PreviousYear, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsCompare.None, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Tham số so sánh (Compare) phải là: 'previous_period', 'previous_year' hoặc 'none'.");

        RuleFor(x => x.TenantSegment)
            .Must(x => string.Equals(x, SystemAnalyticsSegment.All, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsSegment.Paid, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x, SystemAnalyticsSegment.Trial, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Phân khúc khách hàng (TenantSegment) phải là: 'all', 'paid' hoặc 'trial'.");

        RuleFor(x => x.ModuleId)
            .GreaterThan(0)
            .When(x => x.ModuleId.HasValue)
            .WithMessage("ModuleId phải lớn hơn 0.");
    }
}
