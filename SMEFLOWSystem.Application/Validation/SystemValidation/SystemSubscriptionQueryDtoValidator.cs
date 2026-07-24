using FluentValidation;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemSubscriptionQueryDtoValidator
    : AbstractValidator<SystemSubscriptionQueryDto>
{
    private static readonly string[] AllowedStatuses =
        [StatusEnum.ModuleActive, StatusEnum.ModuleTrial, StatusEnum.ModuleSuspended];

    public SystemSubscriptionQueryDtoValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SearchTenant).MaximumLength(200);
        RuleFor(x => x.ModuleId).GreaterThan(0).When(x => x.ModuleId.HasValue);
        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status)
                || AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Subscription status is not supported.");
        RuleFor(x => x.ExpiringTo)
            .GreaterThanOrEqualTo(x => x.ExpiringFrom)
            .When(x => x.ExpiringFrom.HasValue && x.ExpiringTo.HasValue);
    }
}
