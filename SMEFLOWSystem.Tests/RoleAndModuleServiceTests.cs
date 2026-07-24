using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.DTOs.RoleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Tests;

public sealed class RoleAndModuleServiceTests
{
    [Fact]
    public async Task SystemRole_CanUpdateDescriptionWithoutChangingNameOrFlag()
    {
        var repository = new RoleRepositoryStub(new Role
        {
            Id = 1,
            Name = "SystemAdmin",
            Description = "old",
            IsSystemRole = true
        });
        var service = new RoleService(repository, null!);

        var role = await service.UpdateRoleAsync(
            1,
            new RoleUpdateDto { Name = "SystemAdmin", Description = "new" });

        Assert.Equal("new", role.Description);
        Assert.Equal("SystemAdmin", role.Name);
        Assert.True(role.IsSystemRole);
    }

    [Fact]
    public async Task SystemRole_CannotBeRenamed()
    {
        var repository = new RoleRepositoryStub(new Role
        {
            Id = 1,
            Name = "SystemAdmin",
            IsSystemRole = true
        });
        var service = new RoleService(repository, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateRoleAsync(
                1,
                new RoleUpdateDto { Name = "Root", Description = string.Empty }));
    }

    [Fact]
    public async Task DeactivateModule_IsIdempotent()
    {
        var repository = new ModuleRepositoryStub(new Module { Id = 1, IsActive = true });
        var service = new ModuleService(null!, repository);

        Assert.True(await service.DeactivateModuleAsync(1));
        Assert.True(await service.DeactivateModuleAsync(1));
        Assert.False(repository.Module.IsActive);
        Assert.Equal(1, repository.UpdateCalls);
    }

    private sealed class RoleRepositoryStub : IRoleRepository
    {
        private readonly Role _role;

        public RoleRepositoryStub(Role role) => _role = role;

        public Task<Role?> AddRoleAsync(Role role) => Task.FromResult<Role?>(role);
        public Task AddAsync(Role role) => Task.CompletedTask;
        public Task<bool> ExistByNameAsync(string name) => Task.FromResult(false);
        public Task<bool> ExistsByNameExceptIdAsync(string name, int excludedRoleId) => Task.FromResult(false);
        public Task<List<Role>> GetAllRolesAsync() => Task.FromResult(new List<Role> { _role });
        public Task<(List<Role> Items, int TotalCount)> GetAllRolesPagingAsync(int pageNumber, int pageSize)
            => Task.FromResult((new List<Role> { _role }, 1));
        public Task<List<Role>> GetByIdsAsync(IEnumerable<int> roleIds)
            => Task.FromResult(new List<Role> { _role });
        public Task<Role?> GetRoleByIdAsync(int id)
            => Task.FromResult<Role?>(id == _role.Id ? _role : null);
        public Task<Role?> GetRoleByNameAsync(string name)
            => Task.FromResult<Role?>(string.Equals(name, _role.Name, StringComparison.Ordinal) ? _role : null);
        public Task<List<User>> GetUsersByRoleIdAsync(int roleId)
            => Task.FromResult(new List<User>());
        public Task<Role?> UpdateRoleAsync(int id, string name, string description)
        {
            if (id != _role.Id)
                return Task.FromResult<Role?>(null);
            _role.Name = name;
            _role.Description = description;
            return Task.FromResult<Role?>(_role);
        }
    }

    private sealed class ModuleRepositoryStub : IModuleRepository
    {
        public ModuleRepositoryStub(Module module) => Module = module;
        public Module Module { get; }
        public int UpdateCalls { get; private set; }

        public Task AddAsync(Module module) => Task.CompletedTask;
        public Task<bool> ExistsByCodeOrShortCodeAsync(string code, string shortCode) => Task.FromResult(false);
        public Task<List<Module>> GetAllActiveAsync()
            => Task.FromResult(Module.IsActive ? new List<Module> { Module } : new List<Module>());
        public Task<List<Module>> GetAllAsync() => Task.FromResult(new List<Module> { Module });
        public Task<Module?> GetByCodeAsync(string code) => Task.FromResult<Module?>(Module);
        public Task<Module?> GetByIdAsync(int id) => Task.FromResult<Module?>(id == Module.Id ? Module : null);
        public Task<List<Module>> GetByIdsAsync(IEnumerable<int> ids)
            => Task.FromResult(ids.Contains(Module.Id) ? new List<Module> { Module } : new List<Module>());
        public Task<Module> UpdateAsync(Module module)
        {
            UpdateCalls++;
            return Task.FromResult(module);
        }
    }
}
