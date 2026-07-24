using System;
using System.Threading;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemTenantAnalyticsService
{
    Task<SystemTenantFinancialSummaryResponseDto> GetTenantFinancialSummaryAsync(
        Guid tenantId,
        SystemAnalyticsPeriodQueryDto query,
        CancellationToken ct = default);
}
