using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IUserService
    {
        Task<IEnumerable<UserDto>> GetUserByNameAsync(string name);
        Task<IEnumerable<UserDto>> GetAllUserAsync();
        Task<UserDto> GetUserByIdAsync(Guid id);
        Task<User> GetUserByEmailAsync(string email);
        Task<UserDto> UpdateAsync(Guid id, UserUpdatedDto user);
        Task UpdatePasswordAsync(Guid id, string password);
        Task<PagedResultDto<UserDto>> GetAllUsersPagingAsync(PagingRequestDto request);
        Task<UserDto> CreateAsync(UserCreatedDto user, Guid tenantId);
        Task<UserDto> InvitedUserAsync(UserCreatedDto user, Guid tenantId);
    }
}
