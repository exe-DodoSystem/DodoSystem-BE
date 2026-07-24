using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemBootstrapResetRequestDtoValidator
    : AbstractValidator<SystemBootstrapResetRequestDto>
{
    public SystemBootstrapResetRequestDtoValidator()
    {
        RuleFor(x => x.Confirmation)
            .Equal(SystemBootstrapResetService.ConfirmationPhrase);
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}
