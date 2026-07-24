using FluentValidation;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;

namespace SMEFLOWSystem.Application.Validation.ModuleValidation;

public sealed class ModuleUpdateDtoValidator : AbstractValidator<ModuleUpdateDto>
{
    public ModuleUpdateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
    }
}
