using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services.System;

namespace SMEFLOWSystem.Tests;

public sealed class SystemTenantServiceTests
{
    [Fact]
    public async Task PageSizeOver100_IsRejected()
    {
        var service = new SystemTenantService(new TenantReadRepositoryStub());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetAllAsync(new SystemTenantQueryDto { PageSize = 101 }));
    }

    [Theory]
    [InlineData("ownerName")]
    [InlineData("drop table")]
    public async Task UnsupportedSort_IsRejected(string sortBy)
    {
        var service = new SystemTenantService(new TenantReadRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAllAsync(new SystemTenantQueryDto { SortBy = sortBy }));
    }

    private sealed class TenantReadRepositoryStub : ISystemTenantReadRepository
    {
        public Task<PagedResultDto<SystemTenantListItemDto>> GetPagedAsync(
            SystemTenantQueryDto query,
            DateOnly today,
            DateTime nowUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(new PagedResultDto<SystemTenantListItemDto>());

        public Task<SystemTenantDetailDto?> GetByIdAsync(
            Guid tenantId,
            DateOnly today,
            CancellationToken cancellationToken)
            => Task.FromResult<SystemTenantDetailDto?>(null);

        public Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
            Guid tenantId,
            SystemTenantUserQueryDto query,
            CancellationToken cancellationToken)
            => Task.FromResult<PagedResultDto<SystemTenantUserDto>?>(null);
    }
}
