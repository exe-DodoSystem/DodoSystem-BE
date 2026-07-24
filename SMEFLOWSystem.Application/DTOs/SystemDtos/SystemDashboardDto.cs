namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemDashboardOverviewDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TrialTenants { get; set; }
    public int PendingPaymentTenants { get; set; }
    public int SuspendedTenants { get; set; }
    public int NewTenantsInPeriod { get; set; }
    public int ExpiringIn7Days { get; set; }
    public int ExpiringIn30Days { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int CancelledSubscriptionsInPeriod { get; set; }
    public int PendingBillingOrders { get; set; }
    public int FailedPaymentsInPeriod { get; set; }
}

public class ModuleUsageStatDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int ActiveCompaniesCount { get; set; }
}

public class ModuleCancellationStatDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int CancelledCompaniesCount { get; set; }
}

public sealed class ModuleExpirationStatDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int ExpiredCompaniesCount { get; set; }
}

public sealed class SystemModuleTrendQueryDto
{
    public int FromMonth { get; set; }
    public int FromYear { get; set; }
    public int ToMonth { get; set; }
    public int ToYear { get; set; }
}

public sealed class SystemModuleTrendPointDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int ActiveCompanies { get; set; }
    public int Cancellations { get; set; }
    public int Expirations { get; set; }
}

public sealed class SystemModuleTrendSeriesDto
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public IReadOnlyList<SystemModuleTrendPointDto> Points { get; set; }
        = Array.Empty<SystemModuleTrendPointDto>();
}

public sealed class SystemModuleTrendDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public IReadOnlyList<SystemModuleTrendSeriesDto> Series { get; set; }
        = Array.Empty<SystemModuleTrendSeriesDto>();
}

public class SystemDashboardStatsDto
{
    public List<ModuleUsageStatDto> ModuleUsageStats { get; set; } = new();
    public List<ModuleCancellationStatDto> ModuleCancellationStats { get; set; } = new();
}
