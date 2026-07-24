using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SMEFLOWSystem.Application.Interfaces.IRepositories;

namespace SMEFLOWSystem.WebAPI.Authorization;

public sealed class ActiveSystemAdminHandler
    : AuthorizationHandler<ActiveSystemAdminRequirement>
{
    private readonly ISystemBootstrapResetRepository _repository;

    public ActiveSystemAdminHandler(ISystemBootstrapResetRepository repository)
    {
        _repository = repository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveSystemAdminRequirement requirement)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            return;

        if (await _repository.IsActiveSystemAdminAsync(userId, CancellationToken.None))
            context.Succeed(requirement);
    }
}
