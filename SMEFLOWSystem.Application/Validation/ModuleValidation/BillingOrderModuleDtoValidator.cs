using FluentValidation;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Validation.ModuleValidation
{
    public class BillingOrderModuleDtoValidator : AbstractValidator<BillingOrderModuleDto>
    {
        public BillingOrderModuleDtoValidator()
        {

            RuleFor(x => x.BillingOrderId)
                .NotEmpty().WithMessage("Amount is required.");

            RuleFor(x => x.ModuleId)
                .GreaterThan(0).WithMessage("ModuleId must be greater than zero.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            RuleFor(x => x.UnitPrice)
                .GreaterThan(0).WithMessage("UnitPrice must be greater than zero.");
        }
    }
}
