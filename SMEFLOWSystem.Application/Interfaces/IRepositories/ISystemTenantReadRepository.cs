using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ISystemTenantReadRepository
{
    Task<PagedResultDto<SystemTenantListItemDto>> GetPagedAsync(
        SystemTenantQueryDto query,
        DateOnly today,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<SystemTenantDetailDto?> GetByIdAsync(
        Guid tenantId,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
        Guid tenantId,
        SystemTenantUserQueryDto query,
        CancellationToken cancellationToken);
}
