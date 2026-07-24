using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemTenantQueryDtoValidator : AbstractValidator<SystemTenantQueryDto>
{
    private static readonly string[] AllowedStatuses =
        ["Active", "Trial", "PendingPayment", "Suspended"];

    private static readonly string[] AllowedSortFields =
        ["name", "status", "createdAt", "subscriptionEndDate"];

    public SystemTenantQueryDtoValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ExpiringInDays)
            .InclusiveBetween(1, 365)
            .When(x => x.ExpiringInDays.HasValue);
        RuleFor(x => x.ModuleId)
            .GreaterThan(0)
            .When(x => x.ModuleId.HasValue);

        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status)
                || AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status is not supported.");

        RuleFor(x => x.SortBy)
            .Must(value => AllowedSortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy is not supported.");

        RuleFor(x => x.SortDirection)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
