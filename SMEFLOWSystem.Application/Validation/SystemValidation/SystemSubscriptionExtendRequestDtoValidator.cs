using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemSubscriptionExtendRequestDtoValidator
    : AbstractValidator<SystemSubscriptionExtendRequestDto>
{
    public SystemSubscriptionExtendRequestDtoValidator()
    {
        RuleFor(x => x.NewEndDate).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
