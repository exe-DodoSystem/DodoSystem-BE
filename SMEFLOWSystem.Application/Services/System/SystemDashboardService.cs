using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.Application.Services.System;

public class SystemDashboardService : ISystemDashboardService
{
    private readonly IModuleSubscriptionRepository _moduleSubscriptionRepository;

    public SystemDashboardService(IModuleSubscriptionRepository moduleSubscriptionRepository)
    {
        _moduleSubscriptionRepository = moduleSubscriptionRepository;
    }

    public async Task<List<ModuleUsageStatDto>> GetModuleUsageStatisticsAsync(int? month, int? year)
    {
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;
        
        var startOfMonth = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

        var subscriptions = await _moduleSubscriptionRepository.GetAllIgnoreTenantAsync();

        var stats = subscriptions
            .Where(s => s.StartDate <= endOfMonth && s.EndDate >= startOfMonth && 
                        (s.Status == StatusEnum.ModuleActive || s.Status == StatusEnum.ModuleTrial))
            .GroupBy(s => new { s.ModuleId, ModuleName = s.Module?.Name ?? "Unknown" })
            .Select(g => new ModuleUsageStatDto
            {
                Month = targetMonth,
                Year = targetYear,
                ModuleId = g.Key.ModuleId,
                ModuleName = g.Key.ModuleName,
                ActiveCompaniesCount = g.Select(x => x.TenantId).Distinct().Count()
            })
            .ToList();

        return stats;
    }

    public async Task<List<ModuleCancellationStatDto>> GetModuleCancellationStatisticsAsync(int? month, int? year)
    {
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;

        var startOfMonth = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

        var subscriptions = await _moduleSubscriptionRepository.GetAllIgnoreTenantAsync();

        var stats = subscriptions
            .Where(s => 
                (s.Status == StatusEnum.ModuleSuspended && s.UpdatedAt >= startOfMonth && s.UpdatedAt <= endOfMonth) ||
                (s.EndDate >= startOfMonth && s.EndDate <= endOfMonth)
            )
            .GroupBy(s => new { s.ModuleId, ModuleName = s.Module?.Name ?? "Unknown" })
            .Select(g => new ModuleCancellationStatDto
            {
                Month = targetMonth,
                Year = targetYear,
                ModuleId = g.Key.ModuleId,
                ModuleName = g.Key.ModuleName,
                CancelledCompaniesCount = g.Select(x => x.TenantId).Distinct().Count()
            })
            .ToList();

        return stats;
    }
}
