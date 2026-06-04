using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SMEFLOWSystem.Application.DTOs.RefreshTokenDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System.Security.Claims;

namespace SMEFLOWSystem.WebAPI.Controllers;

[Route("api/refresh-tokens")]
[ApiController]
public class RefreshTokenController : ControllerBase
{
    private readonly IRefreshTokenService _refreshTokenService;

    public RefreshTokenController(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }
    /// <summary>Lấy danh sách tất cả Refresh Token của user đang đăng nhập</summary>

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetAllTokenByMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Không tìm thấy user" });

        try
        {
            var listToken = await _refreshTokenService.GetAllByUserIdAsync(userId);
            return Ok(listToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // TenantAdmin can view tokens of a user in the same tenant.
    /// <summary>[TenantAdmin] Lấy danh sách Refresh Token của một user bất kỳ trong Tenant</summary>
    [Authorize(Policy = PolicyNames.TenantAdmin)]
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetAllTokenByUserId([FromRoute] Guid userId)
    {
        try
        {
            var listToken = await _refreshTokenService.GetAllByUserIdAsync(userId);
            return Ok(listToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Issue refresh token for current user (used after login if needed)
    /// <summary>Cấp Refresh Token mới cho user đang đăng nhập</summary>
    [Authorize]
    [HttpPost("issue")]
    public async Task<IActionResult> Issue()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Không tìm thấy user" });

        try
        {
            var result = await _refreshTokenService.IssueAsync(userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Làm mới Access Token bằng Refresh Token</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        var (success, response, message) = await _refreshTokenService.RefreshAsync(request);

        if (!success)
            return Unauthorized(new { error = message });

        return Ok(response);
    }
    /// <summary>Đăng xuất (Thu hồi tất cả Refresh Token của user hiện tại)</summary>

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Không tìm thấy user" });

        try
        {
            await _refreshTokenService.RevokeAllAsync(userId, "Logout");
            return Ok(new { message = "Logout thành công" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
