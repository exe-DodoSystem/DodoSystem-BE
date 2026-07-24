using System;
using System.Collections.Generic;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemRevenueForecastActualPointDto
{
    public string BucketStart { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public sealed class SystemRevenueForecastPointDto
{
    public string BucketStart { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}

public sealed class SystemRevenueForecastResponseDto
{
    public string Method { get; set; } = "LinearTrend";
    public string TrainingFrom { get; set; } = string.Empty;
    public string TrainingTo { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public string Granularity { get; set; } = "month";
    public List<SystemRevenueForecastActualPointDto> ActualPoints { get; set; } = new();
    public List<SystemRevenueForecastPointDto> ForecastPoints { get; set; } = new();
    public SystemAnalyticsMetaDto Meta { get; set; } = new();
}
