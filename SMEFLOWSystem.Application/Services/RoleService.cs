using AutoMapper;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.RoleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IMapper _mapper;

        public RoleService(IRoleRepository roleRepository, IMapper mapper)
        {
            _roleRepository = roleRepository;
            _mapper = mapper;
        }


        public async Task<Role> AddRoleAsync(RoleCreateDto role)
        {
            ArgumentNullException.ThrowIfNull(role);
            var normalizedName = role.Name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentException("Role name is required.", nameof(role));
            if (IsProtectedRoleName(normalizedName))
                throw new InvalidOperationException("Không thể tạo custom role trùng tên system role.");
            if (await _roleRepository.ExistByNameAsync(normalizedName))
                throw new InvalidOperationException("Role name already exists.");

            var newRole = new Role
            {
                Name = normalizedName,
                Description = role.Description?.Trim() ?? string.Empty,
                IsSystemRole = false
            };
            await _roleRepository.AddRoleAsync(newRole);
            return newRole;
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            var roles = await _roleRepository.GetAllRolesAsync();
            return _mapper.Map<IEnumerable<RoleDto>>(roles);
        }

        public async Task<PagedResultDto<RoleDto>> GetAllRolesPagingAsync(PagingRequestDto request)
        {
            var (items, totalCount) = await _roleRepository.GetAllRolesPagingAsync(request.PageNumber, request.PageSize);    

            var roleDtos = _mapper.Map<IEnumerable<RoleDto>>(items);
            return new PagedResultDto<RoleDto>
            {
                Items = roleDtos,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        public async Task<Role> GetRoleByIdAsync(int id)
        {
            var  role = await _roleRepository.GetRoleByIdAsync(id);
            if (role == null)
            {
                throw new ArgumentException($"Role with id {id} not found");
            }
            return role;
        }

        public async Task<Role> UpdateRoleAsync(int id, RoleUpdateDto updatedDto)
        {
            ArgumentNullException.ThrowIfNull(updatedDto);
            var role = await _roleRepository.GetRoleByIdAsync(id)
                ?? throw new KeyNotFoundException($"Role with id {id} not found.");

            var normalizedName = updatedDto.Name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentException("Role name is required.", nameof(updatedDto));

            var protectedRole = role.IsSystemRole == true || IsProtectedRoleName(role.Name);
            if (protectedRole
                && !string.Equals(role.Name, normalizedName, StringComparison.Ordinal))
                throw new InvalidOperationException("Không thể đổi tên system role.");

            if (!string.Equals(role.Name, normalizedName, StringComparison.OrdinalIgnoreCase)
                && IsProtectedRoleName(normalizedName))
                throw new InvalidOperationException("Không thể dùng tên dành riêng cho system role.");

            if (await _roleRepository.ExistsByNameExceptIdAsync(normalizedName, id))
                throw new InvalidOperationException("Role name already exists.");

            return await _roleRepository.UpdateRoleAsync(
                    id,
                    protectedRole ? role.Name : normalizedName,
                    updatedDto.Description?.Trim() ?? string.Empty)
                ?? throw new KeyNotFoundException($"Role with id {id} not found.");
        }

        private static bool IsProtectedRoleName(string name)
        {
            var protectedNames = new[]
            {
                RoleConstants.SystemAdmin,
                RoleConstants.TenantAdmin,
                RoleConstants.HrManager,
                RoleConstants.Manager,
                RoleConstants.Employee
            };
            return protectedNames.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
