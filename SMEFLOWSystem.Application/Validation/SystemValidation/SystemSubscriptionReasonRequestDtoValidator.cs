using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemSubscriptionReasonRequestDtoValidator
    : AbstractValidator<SystemSubscriptionReasonRequestDto>
{
    public SystemSubscriptionReasonRequestDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
