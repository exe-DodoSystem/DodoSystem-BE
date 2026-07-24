using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

public sealed class SystemOperationsHealthComponentDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string? Description { get; set; }
}

public sealed class SystemOperationsHealthResponseDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public long DurationMs { get; set; }
    public List<SystemOperationsHealthComponentDto> Components { get; set; } = new();
}
