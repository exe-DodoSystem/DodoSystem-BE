using FluentValidation;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Validation.ModuleValidation
{
    public class ModuleCreateDtoValidation : AbstractValidator<ModuleCreateDto>
    {
        public ModuleCreateDtoValidation() { 
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Module code is required.");

            RuleFor(x => x.ShortCode)
                .NotEmpty().WithMessage("Module short code is required.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Module name is required.");

            RuleFor(x => x.MonthlyPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Monthly price must be greater than or equal to zero.");
        }
    }
}
