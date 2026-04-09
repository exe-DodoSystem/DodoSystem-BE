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
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize(Roles = "TenantAdmin")]
        [HttpPut("{userId:guid}/role")]
        public async Task<IActionResult> SetUserRole([FromRoute] Guid userId, [FromBody] UserSetRoleDto request)
        {
            try
            {
                await _userService.SetUserRoleAsync(userId, request.RoleIds);
                return Ok(new { message = "Cập nhật role thành công" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("me/avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "Không tìm thấy user" });

            if (avatar == null || avatar.Length == 0)
                return BadRequest(new { error = "Vui lòng chọn ảnh" });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(avatar.ContentType.ToLowerInvariant()))
                return BadRequest(new { error = "Chỉ hỗ trợ ảnh JPEG, PNG, WEBP" });

            const long maxSize = 5 * 1024 * 1024; // 5MB
            if (avatar.Length > maxSize)
                return BadRequest(new { error = "Ảnh không được vượt quá 5MB" });

            try
            {
                using var stream = avatar.OpenReadStream();
                var result = await _userService.UpdateAvatarAsync(userId, stream, avatar.FileName);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
