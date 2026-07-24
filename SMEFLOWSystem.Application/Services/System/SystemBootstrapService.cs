using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Application.Services.System;

public class SystemBootstrapService : ISystemBootstrapService
{
    private readonly ITransaction _transaction;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;

    public SystemBootstrapService(
        ITransaction transaction,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository)
    {
        _transaction = transaction;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
    }

    public async Task<(Guid tenantId, Guid userId)> BootstrapAsync(SystemBootstrapRequestDto request)
    {
        if (request == null)
            throw new ArgumentException("Request không hợp lệ");

        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var fullName = request.FullName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Email và Password là bắt buộc");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("FullName là bắt buộc");

        var systemRole = await _roleRepository.GetRoleByNameAsync("SystemAdmin");
        if (systemRole == null)
            throw new InvalidOperationException("Thiếu role SystemAdmin. Hãy chạy seed roles trước.");

        var alreadyBootstrapped = await _userRoleRepository.AnyUserHasRoleIgnoreTenantAsync(systemRole.Id);
        if (alreadyBootstrapped)
            throw new InvalidOperationException("Hệ thống đã được bootstrap trước đó");

        var emailExists = await _userRepository.IsEmailExistAsync(email);
        if (emailExists)
            throw new InvalidOperationException("Email đã tồn tại");

        Guid systemTenantId = Guid.Empty;
        Guid systemUserId = Guid.Empty;

        await _transaction.ExecuteAsync(async () =>
        {
            var now = DateTime.UtcNow;

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = SystemTenantConstants.Name,
                Status = StatusEnum.TenantActive,
                SubscriptionEndDate = null,
                OwnerUserId = null,
                CreatedAt = now,
                IsDeleted = false
            };
            await _tenantRepository.AddAsync(tenant);

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                FullName = fullName,
                Email = email,
                Phone = request.Phone ?? string.Empty,
                PasswordHash = AuthHelper.HashPassword(password),
                IsActive = true,
                IsVerified = true,
                CreatedAt = now,
                IsDeleted = false
            };
            await _userRepository.AddAsync(user);

            var userRole = new UserRole
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = systemRole.Id
            };
            await _userRoleRepository.AddUserRoleAsync(userRole);

            tenant.OwnerUserId = user.Id;
            await _tenantRepository.UpdateIgnoreTenantAsync(tenant);

            systemTenantId = tenant.Id;
            systemUserId = user.Id;
        });

        return (systemTenantId, systemUserId);
    }
}
