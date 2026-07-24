using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;
using System.Security.Claims;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/bootstrap")]
[ApiController]
public class SystemBootstrapController : ControllerBase
{
    private readonly ISystemBootstrapService _bootstrapService;
    private readonly ISystemBootstrapResetService _resetService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemBootstrapController> _logger;

    public SystemBootstrapController(
        ISystemBootstrapService bootstrapService,
        ISystemBootstrapResetService resetService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<SystemBootstrapController> logger)
    {
        _bootstrapService = bootstrapService;
        _resetService = resetService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
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

    /// <summary>[SystemAdmin] Reset bootstrap identity trong môi trường được bật maintenance gate</summary>
    [HttpDelete("reset")]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
    public async Task<IActionResult> Reset(
        [FromBody] SystemBootstrapResetRequestDto request,
        CancellationToken cancellationToken)
    {
        var nonProductionEnvironmentAllowed =
            _environment.IsDevelopment() || _environment.IsStaging();
        var productionEnvironmentAllowed =
            _environment.IsProduction()
            && _configuration.GetValue<bool>("SystemBootstrap:AllowProductionReset");
        var environmentAllowed =
            nonProductionEnvironmentAllowed || productionEnvironmentAllowed;
        var configAllowed = _configuration.GetValue<bool>("SystemBootstrap:AllowReset");
        if (!environmentAllowed || !configAllowed)
            return NotFound();

        var actorValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorValue, out var actorUserId))
            return Forbid();

        _logger.LogWarning(
            "BOOTSTRAP_RESET_REQUESTED ActorUserId={ActorUserId} Environment={Environment} IP={IP}",
            actorUserId,
            _environment.EnvironmentName,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        var result = await _resetService.ResetAsync(actorUserId, request, cancellationToken);
        if (result.Succeeded)
        {
            return Ok(new SystemBootstrapResetResponseDto());
        }

        _logger.LogWarning(
            "BOOTSTRAP_RESET_REFUSED ReasonCode={ReasonCode} ActorUserId={ActorUserId} Environment={Environment}",
            result.ErrorCode,
            actorUserId,
            _environment.EnvironmentName);

        return result.ErrorCode switch
        {
            "INVALID_CONFIRMATION" or "INVALID_PASSWORD" => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "System bootstrap reset validation failed",
                detail: result.Message),
            "INVALID_TARGET" => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "System bootstrap reset forbidden",
                detail: result.Message),
            "DEPENDENCIES_FOUND" or "TARGET_CHANGED" => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "System bootstrap reset refused",
                detail: result.Message),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "System bootstrap reset failed")
        };
    }
}
