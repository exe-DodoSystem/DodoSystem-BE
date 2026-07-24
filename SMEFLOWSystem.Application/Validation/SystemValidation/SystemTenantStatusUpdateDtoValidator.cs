using FluentValidation;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemTenantStatusUpdateDtoValidator
    : AbstractValidator<SystemTenantStatusUpdateDto>
{
    public SystemTenantStatusUpdateDtoValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => string.Equals(status?.Trim(), StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status?.Trim(), StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be Active or Suspended.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => string.Equals(
                x.Status?.Trim(),
                StatusEnum.TenantSuspended,
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Reason is required when suspending a tenant.");
    }
}
