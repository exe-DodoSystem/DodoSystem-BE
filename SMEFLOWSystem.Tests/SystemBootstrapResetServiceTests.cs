using Microsoft.Extensions.Logging.Abstractions;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemBootstrapResetServiceTests
{
    [Fact]
    public async Task WrongConfirmation_IsRejectedBeforeRepositoryAccess()
    {
        var repository = new BootstrapResetRepositoryStub();
        var service = new SystemBootstrapResetService(
            repository,
            new TransactionStub(),
            NullLogger<SystemBootstrapResetService>.Instance);

        var result = await service.ResetAsync(
            Guid.NewGuid(),
            new SystemBootstrapResetRequestDto
            {
                Confirmation = "wrong",
                CurrentPassword = "secret"
            });

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CONFIRMATION", result.ErrorCode);
        Assert.Equal(0, repository.FindCalls);
    }

    [Fact]
    public async Task ActorWithoutValidSystemTarget_IsForbidden()
    {
        var repository = new BootstrapResetRepositoryStub();
        var service = new SystemBootstrapResetService(
            repository,
            new TransactionStub(),
            NullLogger<SystemBootstrapResetService>.Instance);

        var result = await service.ResetAsync(
            Guid.NewGuid(),
            new SystemBootstrapResetRequestDto
            {
                Confirmation = SystemBootstrapResetService.ConfirmationPhrase,
                CurrentPassword = "secret"
            });

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_TARGET", result.ErrorCode);
    }

    private sealed class TransactionStub : ITransaction
    {
        public Task ExecuteAsync(Func<Task> action) => action();
    }

    private sealed class BootstrapResetRepositoryStub : ISystemBootstrapResetRepository
    {
        public int FindCalls { get; private set; }

        public Task<SystemBootstrapResetTarget?> FindResetTargetAsync(
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            FindCalls++;
            return Task.FromResult<SystemBootstrapResetTarget?>(null);
        }

        public Task<SystemBootstrapDependencyCounts> GetDependencyCountsAsync(
            SystemBootstrapResetTarget target,
            CancellationToken cancellationToken)
            => Task.FromResult(new SystemBootstrapDependencyCounts());

        public Task DeleteBootstrapIdentityAsync(
            SystemBootstrapResetTarget target,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> IsActiveSystemAdminAsync(
            Guid userId,
            CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
