namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public sealed record SystemBootstrapResetTarget(
    Guid TenantId,
    Guid UserId,
    int SystemAdminRoleId,
    string PasswordHash);

public sealed class SystemBootstrapDependencyCounts
{
    public IReadOnlyList<string> OccupiedResources { get; init; } = Array.Empty<string>();
    public bool HasDependencies => OccupiedResources.Count > 0;
}

public interface ISystemBootstrapResetRepository
{
    Task<SystemBootstrapResetTarget?> FindResetTargetAsync(
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<SystemBootstrapDependencyCounts> GetDependencyCountsAsync(
        SystemBootstrapResetTarget target,
        CancellationToken cancellationToken);

    Task DeleteBootstrapIdentityAsync(
        SystemBootstrapResetTarget target,
        CancellationToken cancellationToken);

    Task<bool> IsActiveSystemAdminAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
