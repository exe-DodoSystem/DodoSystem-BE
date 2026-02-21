using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IUserRepository
    {
        Task<User?> AddUserAsync(User user);
        Task<bool> IsEmailExistAsync(string email);
        Task<List<User>> GetUserByNameAsync(string name);
        Task<User?> GetUserByEmailAsync(string email);
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User?> UpdateUserAsync(User user);
        Task<User?> UpdatePasswordAsync(Guid id, string password);
        Task<User?> UpdatePasswordIgnoreTenantAsync(Guid id, string password);
        Task<PagedResultDto<User>> GetAllUserPagingAsync(PagingRequestDto request);
        Task<bool?> CheckUserIsDeleted(Guid id);
        Task<List<Role>> GetRolesByUserIdAsync(Guid userId);
        Task<bool> AddRoleToUserAsync(Guid userId, int roleId);
        Task<bool> RemoveRoleFromUserAsync(Guid userId, int roleId);
        Task AddAsync(User user);
        Task<User> GetOwnerUserByIdAsync(Guid? ownerUserId);
        Task<User?> GetByIdIgnoreTenantAsync(Guid id);
        Task<User?> UpdateUserIgnoreTenantAsync(User user);
    }
}
