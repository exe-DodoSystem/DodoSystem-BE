using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using ShareKernel.Common.Enum;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemBillingOrderQueryDtoValidator
    : AbstractValidator<SystemBillingOrderQueryDto>
{
    private static readonly string[] SortFields =
        ["billingDate", "createdAt", "finalAmount", "billingOrderNumber"];
    private static readonly string[] PaymentStatuses =
        [StatusEnum.PaymentPending, StatusEnum.PaymentPaid, StatusEnum.PaymentFailed, StatusEnum.OrderCancelled];
    private static readonly string[] OrderStatuses =
        [StatusEnum.OrderPending, StatusEnum.OrderPaid, StatusEnum.OrderCancelled, StatusEnum.OrderFailed, StatusEnum.OrderCompleted];

    public SystemBillingOrderQueryDtoValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.PaymentStatus)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || PaymentStatuses.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("PaymentStatus is not supported.");
        RuleFor(x => x.Status)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || OrderStatuses.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status is not supported.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue);
        RuleFor(x => x.SortBy)
            .Must(value => SortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy is not supported.");
        RuleFor(x => x.SortDirection)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
