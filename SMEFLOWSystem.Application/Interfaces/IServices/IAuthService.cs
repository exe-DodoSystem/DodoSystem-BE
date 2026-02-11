using SMEFLOWSystem.Application.DTOs.AuthDtos;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IAuthService
    {
        Task<bool> RegisterTenantAsync(RegisterRequestDto request);
        Task<LoginUserDto> LoginAsync(LoginRequestDto request);
    }
}
