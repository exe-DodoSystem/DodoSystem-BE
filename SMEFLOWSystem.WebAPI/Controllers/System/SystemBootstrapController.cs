using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/bootstrap")]
[ApiController]
public class SystemBootstrapController : ControllerBase
{
    private readonly ISystemBootstrapService _bootstrapService;

    public SystemBootstrapController(
        ISystemBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
    }

    // NOTE: Intentionally AllowAnonymous for first-time bootstrap.
    /// <summary>Khởi tạo Tenant Admin đầu tiên (Chỉ dùng được khi hệ thống chưa có Admin nào)</summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Bootstrap([FromBody] SystemBootstrapRequestDto request)
    {
        try
        {
            var (tenantId, userId) = await _bootstrapService.BootstrapAsync(request);

            return Ok(new
            {
                tenantId,
                userId,
                message = "Bootstrap thành công. Hãy login để lấy token."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // For bootstrapping we treat InvalidOperation as Conflict or Server Misconfig.
            if (ex.Message.Contains("Thiếu role SystemAdmin", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            return Conflict(new { error = ex.Message });
        }
    }
}
