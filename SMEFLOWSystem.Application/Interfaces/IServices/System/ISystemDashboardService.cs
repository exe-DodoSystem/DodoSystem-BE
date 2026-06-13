using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemDashboardService
{
    Task<List<ModuleUsageStatDto>> GetModuleUsageStatisticsAsync(int? month, int? year);
    Task<List<ModuleCancellationStatDto>> GetModuleCancellationStatisticsAsync(int? month, int? year);
}
