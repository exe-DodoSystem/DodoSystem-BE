using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemPaymentTransactionQueryDtoValidator
    : AbstractValidator<SystemPaymentTransactionQueryDto>
{
    private static readonly string[] Gateways = ["VNPay", "SePay"];
    private static readonly string[] Statuses = ["Success", "Failed"];

    public SystemPaymentTransactionQueryDtoValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Gateway)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Gateways.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Gateway is not supported.");
        RuleFor(x => x.Status)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Statuses.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status is not supported.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
