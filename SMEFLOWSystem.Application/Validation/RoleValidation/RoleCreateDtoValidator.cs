using FluentValidation;
using SMEFLOWSystem.Application.DTOs.RoleDtos;

namespace SMEFLOWSystem.Application.Validation.RoleValidation;

public sealed class RoleCreateDtoValidator : AbstractValidator<RoleCreateDto>
{
    public RoleCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
