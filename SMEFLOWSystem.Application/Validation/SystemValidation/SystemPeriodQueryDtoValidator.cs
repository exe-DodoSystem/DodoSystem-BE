using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemPeriodQueryDtoValidator : AbstractValidator<SystemPeriodQueryDto>
{
    public SystemPeriodQueryDtoValidator()
    {
        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .When(x => x.Month.HasValue);

        RuleFor(x => x.Year)
            .InclusiveBetween(2020, DateTime.UtcNow.Year + 1)
            .When(x => x.Year.HasValue);
    }
}
