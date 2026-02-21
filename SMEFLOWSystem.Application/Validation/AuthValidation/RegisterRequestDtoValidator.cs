using FluentValidation;
using SMEFLOWSystem.Application.DTOs.AuthDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Validation.AuthValidation
{
    public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
    {
        public RegisterRequestDtoValidator() {
            RuleFor(x => x.AdminEmail)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email is invalid");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required");

            RuleFor(x => x.AdminFullName)
                .NotEmpty().WithMessage("Full name is required");

            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("Company name is required");

            RuleFor(x => x.ModuleIds)
                .NotEmpty().WithMessage("At least one module must be selected");
        }
    }
}
