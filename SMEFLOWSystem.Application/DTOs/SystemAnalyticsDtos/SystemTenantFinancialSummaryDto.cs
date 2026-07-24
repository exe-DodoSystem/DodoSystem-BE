using System;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemTenantSubscriptionSummaryDto
{
    public int Active { get; set; }
    public int Trial { get; set; }
    public int ExpiringIn30Days { get; set; }
}

public sealed class SystemTenantFinancialSummaryResponseDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Estimated MRR (uses current catalog price). Always accompanied by mrrStatus=Estimated.</summary>
    public decimal CurrentMrr { get; set; }

    public decimal LifetimeCollectedRevenue { get; set; }
    public decimal CollectedRevenueInPeriod { get; set; }
    public decimal OutstandingAmount { get; set; }

    public DateTime? LastSuccessfulPaymentAt { get; set; }
    public DateTime? LastFailedPaymentAt { get; set; }

    /// <summary>Average days from BillingDate to ProcessedAt. Null when no completed payments exist.</summary>
    public double? AveragePaymentDelayDays { get; set; }

    public SystemTenantSubscriptionSummaryDto Subscriptions { get; set; } = new();
    public SystemAnalyticsMetaDto Meta { get; set; } = new();
}
