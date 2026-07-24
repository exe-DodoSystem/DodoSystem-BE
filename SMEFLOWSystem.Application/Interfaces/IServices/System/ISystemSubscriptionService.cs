using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemSubscriptionService
{
    Task<PagedResultDto<SystemSubscriptionDto>> GetAllAsync(
        SystemSubscriptionQueryDto query,
        CancellationToken cancellationToken = default);

    Task<SystemSubscriptionCommandResultDto> ExtendAsync(
        Guid subscriptionId,
        SystemSubscriptionExtendRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SystemSubscriptionCommandResultDto> SuspendAsync(
        Guid subscriptionId,
        SystemSubscriptionReasonRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SystemSubscriptionCommandResultDto> ReactivateAsync(
        Guid subscriptionId,
        SystemSubscriptionReasonRequestDto request,
        CancellationToken cancellationToken = default);
}
