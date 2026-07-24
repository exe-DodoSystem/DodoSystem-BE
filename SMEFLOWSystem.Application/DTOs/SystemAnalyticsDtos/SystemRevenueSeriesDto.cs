using System;
using System.Collections.Generic;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemRevenueSeriesPointDto
{
    public string BucketStart { get; set; } = string.Empty;
    public decimal InvoicedRevenue { get; set; }
    public decimal CollectedRevenue { get; set; }
    public decimal? RefundedAmount { get; set; }
    public decimal OutstandingCreated { get; set; }
    public decimal? MrrSnapshot { get; set; }
}

public sealed class SystemRevenueSeriesResponseDto
{
    public List<SystemRevenueSeriesPointDto> Points { get; set; } = new();
    public List<SystemRevenueSeriesPointDto>? PreviousPoints { get; set; }
    public SystemAnalyticsMetaDto Meta { get; set; } = new();
}
