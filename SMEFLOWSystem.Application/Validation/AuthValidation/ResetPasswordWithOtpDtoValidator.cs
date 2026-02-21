using FluentValidation;
using SMEFLOWSystem.Application.DTOs.AuthDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Validation.AuthValidation
{
    public class ResetPasswordWithOtpDtoValidator : AbstractValidator<ResetPasswordWithOtpDto>
    {
        public ResetPasswordWithOtpDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email is invalid");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required");
        }
    }
}
