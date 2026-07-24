using System;
using Microsoft.Extensions.Options;
using Xunit;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Helpers.System;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Validation.SystemValidation;

namespace SMEFLOWSystem.Tests;

public sealed class SystemAnalyticsPeriodTests
{
    private readonly SystemAnalyticsOptions _options = new()
    {
        DefaultRangeDays = 30,
        MaxRangeMonths = 24
    };

    private IOptions<SystemAnalyticsOptions> WrappedOptions => Options.Create(_options);

    [Fact]
    public void PaymentStatusClassifier_IdentifiesSuccessStatusCorrectly()
    {
        Assert.True(PaymentStatusClassifier.IsSuccessful("Success"));
        Assert.True(PaymentStatusClassifier.IsSuccessful("succeeded"));
        Assert.True(PaymentStatusClassifier.IsSuccessful("Settled"));
        Assert.True(PaymentStatusClassifier.IsSuccessful("Paid"));
        
        Assert.False(PaymentStatusClassifier.IsSuccessful("Pending"));
        Assert.False(PaymentStatusClassifier.IsSuccessful("Failed"));
        Assert.False(PaymentStatusClassifier.IsSuccessful(null));
        Assert.False(PaymentStatusClassifier.IsSuccessful(""));
    }

    [Fact]
    public void PaymentStatusClassifier_IdentifiesFailedStatusCorrectly()
    {
        Assert.True(PaymentStatusClassifier.IsFailed("Failed"));
        Assert.True(PaymentStatusClassifier.IsFailed("failed"));
        
        Assert.False(PaymentStatusClassifier.IsFailed("Success"));
        Assert.False(PaymentStatusClassifier.IsFailed("Pending"));
        Assert.False(PaymentStatusClassifier.IsFailed(null));
    }

    [Fact]
    public void PaymentStatusClassifier_IdentifiesTerminalStatusCorrectly()
    {
        Assert.True(PaymentStatusClassifier.IsKnownTerminalStatus("Success"));
        Assert.True(PaymentStatusClassifier.IsKnownTerminalStatus("Failed"));
        Assert.False(PaymentStatusClassifier.IsKnownTerminalStatus("Pending"));
    }

    [Fact]
    public void AnalyticsPeriodResolver_ConvertsLocalToUtcCorrectly()
    {
        var query = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 7, 1),
            To = new DateOnly(2026, 7, 31),
            Timezone = "Asia/Ho_Chi_Minh",
            Compare = "none"
        };

        var resolved = AnalyticsPeriodResolver.Resolve(query, _options);

        Assert.Equal(new DateOnly(2026, 7, 1), resolved.From);
        Assert.Equal(new DateOnly(2026, 7, 31), resolved.To);

        // Asia/Ho_Chi_Minh is UTC+7. 
        // local 2026-07-01 00:00:00 -> UTC 2026-06-30 17:00:00
        Assert.Equal(new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc), resolved.StartUtc);
        
        // local 2026-08-01 00:00:00 (exclusive) -> UTC 2026-07-31 17:00:00
        Assert.Equal(new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc), resolved.EndExclusiveUtc);
    }

    [Fact]
    public void AnalyticsPeriodResolver_DefaultsToThirtyInclusiveDays()
    {
        var query = new SystemAnalyticsPeriodQueryDto
        {
            Compare = SystemAnalyticsCompare.None
        };

        var resolved = AnalyticsPeriodResolver.Resolve(
            query,
            _options,
            new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 6, 25), resolved.From);
        Assert.Equal(new DateOnly(2026, 7, 24), resolved.To);
        Assert.Equal(30, resolved.To.DayNumber - resolved.From.DayNumber + 1);
    }

    [Fact]
    public void AnalyticsPeriodResolver_ResolvesPreviousPeriodCorrectly()
    {
        var query = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 7, 10),
            To = new DateOnly(2026, 7, 20),
            Timezone = "Asia/Ho_Chi_Minh",
            Compare = "previous_period"
        };

        var resolved = AnalyticsPeriodResolver.Resolve(query, _options);

        // Duration is 11 days (10 to 20 inclusive)
        // Previous From = 10 - 11 = June 29
        // Previous To = 20 - 11 = July 9
        Assert.Equal(new DateOnly(2026, 6, 29), resolved.PreviousFrom);
        Assert.Equal(new DateOnly(2026, 7, 9), resolved.PreviousTo);

        Assert.NotNull(resolved.PreviousStartUtc);
        Assert.NotNull(resolved.PreviousEndExclusiveUtc);
        
        // local 2026-06-29 00:00:00 -> UTC 2026-06-28 17:00:00
        Assert.Equal(new DateTime(2026, 6, 28, 17, 0, 0, DateTimeKind.Utc), resolved.PreviousStartUtc.Value);
        // local 2026-07-10 00:00:00 -> UTC 2026-07-09 17:00:00
        Assert.Equal(new DateTime(2026, 7, 9, 17, 0, 0, DateTimeKind.Utc), resolved.PreviousEndExclusiveUtc.Value);
    }

    [Fact]
    public void PeriodQueryDtoValidator_ValidatesCorrectRange()
    {
        var validator = new SystemAnalyticsPeriodQueryDtoValidator(WrappedOptions);

        var validDto = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 6, 30),
            Timezone = "Asia/Ho_Chi_Minh",
            Currency = "VND",
            Compare = "previous_period",
            TenantSegment = "all"
        };
        var result = validator.Validate(validDto);
        Assert.True(result.IsValid);

        // invalid: from > to
        var invalidDatesDto = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 6, 30),
            To = new DateOnly(2026, 1, 1)
        };
        var resultDates = validator.Validate(invalidDatesDto);
        Assert.False(resultDates.IsValid);

        // invalid: range > 24 months
        var tooLongDto = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2026, 3, 1)
        };
        var resultTooLong = validator.Validate(tooLongDto);
        Assert.False(resultTooLong.IsValid);

        // invalid: unsupported timezone
        var invalidTzDto = new SystemAnalyticsPeriodQueryDto
        {
            Timezone = "America/New_York"
        };
        var resultTz = validator.Validate(invalidTzDto);
        Assert.False(resultTz.IsValid);
    }

    [Fact]
    public void PeriodQueryDtoValidator_ValidatesRangesAfterApplyingMissingDefaults()
    {
        var validator = new SystemAnalyticsPeriodQueryDtoValidator(WrappedOptions);
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                AnalyticsPeriodResolver.GetTimeZone(SystemAnalyticsOptions.SupportedTimezone)));

        var futureFrom = validator.Validate(new SystemAnalyticsPeriodQueryDto
        {
            From = localToday.AddDays(1)
        });
        var oldTo = validator.Validate(new SystemAnalyticsPeriodQueryDto
        {
            To = localToday.AddDays(-30)
        });

        Assert.False(futureFrom.IsValid);
        Assert.Contains(futureFrom.Errors, error => error.PropertyName == nameof(SystemAnalyticsPeriodQueryDto.From));
        Assert.False(oldTo.IsValid);
        Assert.Contains(oldTo.Errors, error => error.PropertyName == nameof(SystemAnalyticsPeriodQueryDto.To));
    }

    [Fact]
    public void PeriodQueryDtoValidator_UsesExactConfiguredMaximumRange()
    {
        var validator = new SystemAnalyticsPeriodQueryDtoValidator(WrappedOptions);

        var exactMaximum = validator.Validate(new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2026, 1, 1)
        });
        var overMaximum = validator.Validate(new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2026, 1, 2)
        });

        Assert.True(exactMaximum.IsValid);
        Assert.False(overMaximum.IsValid);
        Assert.Contains(overMaximum.Errors, error => error.PropertyName == nameof(SystemAnalyticsPeriodQueryDto.To));
    }

    [Fact]
    public void PeriodQueryDtoValidator_RejectsUnknownCurrency()
    {
        var validator = new SystemAnalyticsPeriodQueryDtoValidator(WrappedOptions);

        var result = validator.Validate(new SystemAnalyticsPeriodQueryDto
        {
            Currency = "USD"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SystemAnalyticsPeriodQueryDto.Currency));
    }

    [Theory]
    [InlineData(31, SystemAnalyticsGranularity.Day)]
    [InlineData(32, SystemAnalyticsGranularity.Week)]
    [InlineData(180, SystemAnalyticsGranularity.Week)]
    [InlineData(181, SystemAnalyticsGranularity.Month)]
    public void AnalyticsPeriodResolver_SelectsDefaultGranularity(int inclusiveDays, string expected)
    {
        var from = new DateOnly(2026, 1, 1);
        var to = from.AddDays(inclusiveDays - 1);

        var granularity = AnalyticsPeriodResolver.ResolveGranularity(null, from, to);

        Assert.Equal(expected, granularity);
    }

    [Fact]
    public void AnalyticsPeriodResolver_RejectsUnknownGranularityAndTimezone()
    {
        Assert.Throws<ArgumentException>(() =>
            AnalyticsPeriodResolver.ResolveGranularity(
                "quarter",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31)));

        Assert.Throws<TimeZoneNotFoundException>(() =>
            AnalyticsPeriodResolver.GetTimeZone("America/New_York"));
    }

    [Fact]
    public void AnalyticsPeriodResolver_BuildsCanonicalSafeMetadata()
    {
        var query = new SystemAnalyticsPeriodQueryDto
        {
            From = new DateOnly(2026, 7, 1),
            To = new DateOnly(2026, 7, 31),
            Timezone = "asia/ho_chi_minh",
            Currency = "vnd",
            Compare = SystemAnalyticsCompare.None
        };
        var period = AnalyticsPeriodResolver.Resolve(query, _options);

        var unavailableMeta = AnalyticsPeriodResolver.BuildMeta(period, query);
        var estimatedMeta = AnalyticsPeriodResolver.BuildMeta(
            period,
            query,
            SystemAnalyticsMrrStatus.Estimated);

        Assert.Equal(SystemAnalyticsOptions.SupportedTimezone, unavailableMeta.Timezone);
        Assert.Equal(SystemAnalyticsOptions.SupportedCurrency, unavailableMeta.Currency);
        Assert.Equal(SystemAnalyticsMrrStatus.Unavailable, unavailableMeta.MrrStatus);
        Assert.Equal(SystemAnalyticsMrrStatus.Estimated, estimatedMeta.MrrStatus);
    }

    [Fact]
    public void EndpointQueryValidators_ValidateSpecificFields()
    {
        var seriesValidator = new SystemRevenueSeriesQueryDtoValidator(WrappedOptions);
        var breakdownValidator = new SystemRevenueBreakdownQueryDtoValidator(WrappedOptions);
        var forecastValidator = new SystemRevenueForecastQueryDtoValidator(WrappedOptions);

        Assert.False(seriesValidator.Validate(new SystemRevenueSeriesQueryDto
        {
            Granularity = "quarter"
        }).IsValid);
        Assert.False(breakdownValidator.Validate(new SystemRevenueBreakdownQueryDto
        {
            Dimension = "unknown",
            Limit = 4
        }).IsValid);
        Assert.False(forecastValidator.Validate(new SystemRevenueForecastQueryDto
        {
            ForecastPeriods = 7,
            Granularity = SystemAnalyticsGranularity.Week
        }).IsValid);

        Assert.True(breakdownValidator.Validate(new SystemRevenueBreakdownQueryDto
        {
            Dimension = SystemAnalyticsDimension.Module,
            Limit = 10
        }).IsValid);
        Assert.True(forecastValidator.Validate(new SystemRevenueForecastQueryDto
        {
            ForecastPeriods = 3,
            Granularity = SystemAnalyticsGranularity.Month
        }).IsValid);
    }
}
