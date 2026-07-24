using FluentValidation;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Validation.SystemValidation;

public sealed class SystemModuleTrendQueryDtoValidator
    : AbstractValidator<SystemModuleTrendQueryDto>
{
    public SystemModuleTrendQueryDtoValidator()
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        RuleFor(x => x.FromMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ToMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.FromYear).InclusiveBetween(2020, maxYear);
        RuleFor(x => x.ToYear).InclusiveBetween(2020, maxYear);
        RuleFor(x => x).Must(query =>
        {
            if (query.FromMonth is < 1 or > 12
                || query.ToMonth is < 1 or > 12
                || query.FromYear < 2020
                || query.ToYear < 2020
                || query.FromYear > maxYear
                || query.ToYear > maxYear)
                return true;
            var from = new DateTime(query.FromYear, query.FromMonth, 1);
            var to = new DateTime(query.ToYear, query.ToMonth, 1);
            var months = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
            return months is >= 1 and <= 24;
        }).WithMessage("Trend range must be ordered and cannot exceed 24 months.");
    }
}
