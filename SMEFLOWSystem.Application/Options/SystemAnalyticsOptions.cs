namespace SMEFLOWSystem.Application.Options;

public sealed class SystemAnalyticsOptions
{
    public const string SectionName = "SystemAnalytics";
    public const string SupportedTimezone = "Asia/Ho_Chi_Minh";
    public const string SupportedCurrency = "VND";

    public string BusinessTimezone { get; set; } = "Asia/Ho_Chi_Minh";
    public int DefaultRangeDays { get; set; } = 30;
    public int MaxRangeMonths { get; set; } = 24;
    public int CacheSeconds { get; set; } = 120;
    public int OrderOverdueGraceHours { get; set; } = 24;
    public int ActionCenterMaxItems { get; set; } = 100;
    public int ForecastMinimumMonths { get; set; } = 6;
    public int ForecastMaximumPeriods { get; set; } = 6;
}
