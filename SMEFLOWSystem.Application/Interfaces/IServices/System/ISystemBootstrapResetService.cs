using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemBootstrapResetService
{
    Task<SystemBootstrapResetResult> ResetAsync(
        Guid actorUserId,
        SystemBootstrapResetRequestDto request,
        CancellationToken cancellationToken = default);
}
