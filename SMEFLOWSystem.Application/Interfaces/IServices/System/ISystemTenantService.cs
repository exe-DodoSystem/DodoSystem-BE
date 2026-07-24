using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SharedKernel.DTOs;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemTenantService
{
    Task<PagedResultDto<SystemTenantListItemDto>> GetAllAsync(
        SystemTenantQueryDto request,
        CancellationToken cancellationToken = default);

    Task<SystemTenantDetailDto?> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
        Guid tenantId,
        SystemTenantUserQueryDto query,
        CancellationToken cancellationToken = default);

    Task<SystemTenantStatusResultDto> ChangeStatusAsync(
        Guid tenantId,
        SystemTenantStatusUpdateDto request,
        CancellationToken cancellationToken = default);
}
