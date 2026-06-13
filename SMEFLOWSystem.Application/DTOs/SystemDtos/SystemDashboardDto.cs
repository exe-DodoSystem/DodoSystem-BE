namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

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

public class SystemDashboardStatsDto
{
    public List<ModuleUsageStatDto> ModuleUsageStats { get; set; } = new();
    public List<ModuleCancellationStatDto> ModuleCancellationStats { get; set; } = new();
}
