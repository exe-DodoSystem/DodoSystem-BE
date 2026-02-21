using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System.Security.Claims;
using SMEFLOWSystem.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly ICurrentTenantService _currentTenantService;

        public UserController(IUserService userService, ICurrentTenantService currentTenantService)
        {
            _userService = userService;
            _currentTenantService = currentTenantService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Không tìm thấy user");

            var user = await _userService.GetUserByIdAsync(userId);
            return Ok(user);

        }

        [Authorize]
        [HttpPost("invite")]
        public async Task<IActionResult> InviteUser([FromBody] UserCreatedDto user)
        {
            var tenantId = _currentTenantService.TenantId;
            if (!tenantId.HasValue)
                return Unauthorized("Không tìm thấy tenant");
            try
            {
                var invitedUser = await _userService.InvitedUserAsync(user, tenantId.Value);
                return Ok(invitedUser);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
