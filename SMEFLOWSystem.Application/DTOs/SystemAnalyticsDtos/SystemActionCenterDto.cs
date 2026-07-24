using System;
using System.Collections.Generic;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemActionCenterCountsDto
{
    public int Critical { get; set; }
    public int Warning { get; set; }
    public int Info { get; set; }
}

public sealed class SystemActionCenterItemDto
{
    /// <summary>Stable ID = "{Type}_{EntityId}"</summary>
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public Guid? EntityId { get; set; }
    /// <summary>FE route, generated from allow-list — never from user input.</summary>
    public string? TargetPath { get; set; }
}

public static class SystemActionCenterItemType
{
    public const string PaymentFailed = "PaymentFailed";
    public const string OrderOverdue = "OrderOverdue";
    public const string SubscriptionExpiring = "SubscriptionExpiring";
    public const string TrialEnding = "TrialEnding";
    public const string TenantSuspended = "TenantSuspended";
}

public static class SystemActionCenterSeverity
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Info = "info";
}

public sealed class SystemActionCenterResponseDto
{
    public SystemActionCenterCountsDto Counts { get; set; } = new();
    public List<SystemActionCenterItemDto> Items { get; set; } = new();
    public SystemAnalyticsMetaDto Meta { get; set; } = new();
}
