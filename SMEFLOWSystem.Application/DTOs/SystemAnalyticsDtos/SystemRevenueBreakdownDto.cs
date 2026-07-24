using System.Collections.Generic;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemRevenueBreakdownItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CollectedRevenue { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public sealed class SystemRevenueBreakdownResponseDto
{
    public decimal TotalCollectedRevenue { get; set; }
    public List<SystemRevenueBreakdownItemDto> Items { get; set; } = new();
    public SystemRevenueBreakdownItemDto? Other { get; set; }
    public SystemAnalyticsMetaDto Meta { get; set; } = new();
}
