using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.Exceptions;
using SMEFLOWSystem.WebAPI.ProblemDetails;
using SMEFLOWSystem.WebAPI.Services;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/operations")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemOperationsController : ControllerBase
{
    private readonly ISystemOperationsHealthService _healthService;
    private readonly ILogger<SystemOperationsController> _logger;

    public SystemOperationsController(
        ISystemOperationsHealthService healthService,
        ILogger<SystemOperationsController> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    /// <summary>[SystemAdmin] Lấy trạng thái an toàn của các dependency hệ thống.</summary>
    [HttpGet("health-summary")]
    [ProducesResponseType<SystemOperationsHealthResponseDto>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHealthSummary(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _healthService.GetHealthSummaryAsync(
                cancellationToken));
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while generating the System Operations health summary.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(
                HttpContext);
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
    }
}
