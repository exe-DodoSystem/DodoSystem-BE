using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ISystemSubscriptionReadRepository
{
    Task<PagedResultDto<SystemSubscriptionDto>> GetPagedAsync(
        SystemSubscriptionQueryDto query,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
