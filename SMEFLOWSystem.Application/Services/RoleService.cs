using AutoMapper;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.RoleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
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


        public async Task<Role> AddRoleAsync(RoleUpdatedDto role)
        {
            if(role == null)
            {
                throw new ArgumentNullException(nameof(role), "Role cannot be null");
            }

            var newRole = _mapper.Map<Role>(role);
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

        public async Task<Role> UpdateRoleAsync(int id, RoleUpdatedDto updatedDto)
        {
            var existingRole = await _roleRepository.ExistByNameAsync(updatedDto.Name);
            if(existingRole)
            {
                throw new ArgumentException($"Role name existed");
            }

            var role = await _roleRepository.UpdateRoleAsync(id, updatedDto.Name, updatedDto.Description, updatedDto.IsSystemRole);
            if(role == null)
            {
                throw new ArgumentException($"Role with id {id} not found");
            }
            return role;
        }

        
    }
}
