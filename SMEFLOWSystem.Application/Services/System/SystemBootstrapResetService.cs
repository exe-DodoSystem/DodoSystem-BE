using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.Application.Services.System;

public sealed class SystemBootstrapResetService : ISystemBootstrapResetService
{
    public const string ConfirmationPhrase = "RESET_SYSTEM_BOOTSTRAP";

    private readonly ISystemBootstrapResetRepository _repository;
    private readonly ITransaction _transaction;
    private readonly ILogger<SystemBootstrapResetService> _logger;

    public SystemBootstrapResetService(
        ISystemBootstrapResetRepository repository,
        ITransaction transaction,
        ILogger<SystemBootstrapResetService> logger)
    {
        _repository = repository;
        _transaction = transaction;
        _logger = logger;
    }

    public async Task<SystemBootstrapResetResult> ResetAsync(
        Guid actorUserId,
        SystemBootstrapResetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null
            || !string.Equals(request.Confirmation, ConfirmationPhrase, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return SystemBootstrapResetResult.Failure(
                "INVALID_CONFIRMATION",
                $"Confirmation must exactly match {ConfirmationPhrase} and CurrentPassword is required.");
        }

        var target = await _repository.FindResetTargetAsync(actorUserId, cancellationToken);
        if (target == null)
            return SystemBootstrapResetResult.Failure(
                "INVALID_TARGET",
                "The authenticated user is not the active owner of the SYSTEM tenant.");

        if (!AuthHelper.VerifyPassword(request.CurrentPassword, target.PasswordHash))
            return SystemBootstrapResetResult.Failure(
                "INVALID_PASSWORD",
                "Current password is invalid.");

        var dependencies = await _repository.GetDependencyCountsAsync(target, cancellationToken);
        if (dependencies.HasDependencies)
        {
            _logger.LogWarning(
                "BOOTSTRAP_RESET_REFUSED ReasonCode={ReasonCode} ActorUserId={ActorUserId} Resources={Resources}",
                "DEPENDENCIES_FOUND",
                actorUserId,
                string.Join(",", dependencies.OccupiedResources));
            return SystemBootstrapResetResult.Failure(
                "DEPENDENCIES_FOUND",
                "Tenant SYSTEM chứa dữ liệu ngoài phạm vi bootstrap và không thể reset tự động.");
        }

        SystemBootstrapResetResult transactionResult = SystemBootstrapResetResult.Success();
        await _transaction.ExecuteAsync(async () =>
        {
            var lockedTarget = await _repository.FindResetTargetAsync(actorUserId, cancellationToken);
            if (lockedTarget == null)
            {
                transactionResult = SystemBootstrapResetResult.Failure(
                    "TARGET_CHANGED",
                    "Bootstrap identity changed before reset.");
                return;
            }

            var currentDependencies = await _repository.GetDependencyCountsAsync(
                lockedTarget,
                cancellationToken);
            if (currentDependencies.HasDependencies)
            {
                transactionResult = SystemBootstrapResetResult.Failure(
                    "DEPENDENCIES_FOUND",
                    "Tenant SYSTEM chứa dữ liệu ngoài phạm vi bootstrap và không thể reset tự động.");
                return;
            }

            await _repository.DeleteBootstrapIdentityAsync(lockedTarget, cancellationToken);
        });

        if (transactionResult.Succeeded)
        {
            _logger.LogInformation(
                "BOOTSTRAP_RESET_SUCCEEDED DeletedUserId={DeletedUserId} DeletedTenantId={DeletedTenantId}",
                target.UserId,
                target.TenantId);
        }

        return transactionResult;
    }
}
