using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/invites")]
public class HrInvitesController : ControllerBase
{
    private readonly IInviteService _inviteService;
    private readonly ICurrentTenantService _currentTenant;

    public HrInvitesController(IInviteService inviteService, ICurrentTenantService currentTenant)
    {
        _inviteService = inviteService;
        _currentTenant = currentTenant;
    }

    [Authorize]
    [HttpPost("send")]
    public async Task<IActionResult> SendInvite([FromBody] HrInviteSendRequestDto request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return StatusCode(403, new { error = "Thiếu TenantId" });

        try {
        
            await _inviteService.SendInviteAsync(tenantId.Value, request.Email, request.RoleId, request.DepartmentId, request.PositionId, request.Message);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
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

    // Unauthenticated onboarding completion (ModuleAccessMiddleware skips unauthenticated requests)
    [AllowAnonymous]
    [HttpGet("validate")]
    public async Task<IActionResult> Validate([FromQuery] string token)
    {
        try
        {
            var invite = await _inviteService.ValidateInviteTokenAsync(token);
            return Ok(new
            {
                invite.Email,
                invite.TenantId,
                invite.RoleId,
                invite.DepartmentId,
                invite.PositionId,
                invite.ExpiryDate,
                invite.IsUsed
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] HrInviteCompleteRequestDto request)
    {
        try
        {
            await _inviteService.CompleteOnboardingAsync(request.Token, request.FullName, request.Password, request.Phone);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
