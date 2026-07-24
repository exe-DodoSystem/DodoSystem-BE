using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public class SystemAnalyticsPeriodQueryDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Currency { get; set; } = "VND";
    public string Compare { get; set; } = "previous_period";
    public int? ModuleId { get; set; }
    public string TenantSegment { get; set; } = "all";
}

public sealed class SystemAnalyticsMetaDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? PreviousFrom { get; set; }
    public string? PreviousTo { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Currency { get; set; } = "VND";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DataThrough { get; set; }
    public string Freshness { get; set; } = "Live";
    public bool ExcludesInternalTenant { get; set; } = true;
    public bool ExcludesTestTenants { get; set; } = false;
    public string MrrStatus { get; set; } = SystemAnalyticsMrrStatus.Unavailable;
    public List<string> Warnings { get; set; } = new();
}

public static class SystemAnalyticsWarningCodes
{
    public const string RefundDataUnavailable = "REFUND_DATA_UNAVAILABLE";
    public const string TestTenantFlagUnavailable = "TEST_TENANT_FLAG_UNAVAILABLE";
    public const string MrrUsesCurrentCatalogPrice = "MRR_USES_CURRENT_CATALOG_PRICE";
    public const string PaymentWithoutProcessedAtExcluded = "PAYMENT_WITHOUT_PROCESSED_AT_EXCLUDED";
    public const string PaymentStatusUnrecognized = "PAYMENT_STATUS_UNRECOGNIZED";
    public const string OrderModuleAllocationUnavailable = "ORDER_MODULE_ALLOCATION_UNAVAILABLE";
    public const string OrderOverdueUsesConfiguredGracePeriod = "ORDER_OVERDUE_USES_CONFIGURED_GRACE_PERIOD";
    public const string ForecastExcludesRefunds = "FORECAST_EXCLUDES_REFUNDS";
    public const string ForecastBasedOnAvailablePaymentHistory = "FORECAST_BASED_ON_AVAILABLE_PAYMENT_HISTORY";
    public const string PaymentDelayDaysNegativeExcluded = "PAYMENT_DELAY_DAYS_NEGATIVE_EXCLUDED";
}

public static class SystemAnalyticsGranularity
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
}

public static class SystemAnalyticsDimension
{
    public const string Module = "module";
    public const string Tenant = "tenant";
    public const string Gateway = "gateway";
}

public static class SystemAnalyticsCompare
{
    public const string PreviousPeriod = "previous_period";
    public const string PreviousYear = "previous_year";
    public const string None = "none";
}

public static class SystemAnalyticsMrrStatus
{
    public const string Estimated = "Estimated";
    public const string Unavailable = "Unavailable";
}

public static class SystemAnalyticsSegment
{
    public const string All = "all";
    public const string Paid = "paid";
    public const string Trial = "trial";
}

public sealed class SystemRevenueSeriesQueryDto : SystemAnalyticsPeriodQueryDto
{
    public string? Granularity { get; set; }
}

public sealed class SystemRevenueBreakdownQueryDto : SystemAnalyticsPeriodQueryDto
{
    public string Dimension { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class SystemRevenueForecastQueryDto : SystemAnalyticsPeriodQueryDto
{
    public int? ForecastPeriods { get; set; }
    public string? Granularity { get; set; }
}
