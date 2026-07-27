using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.ProblemDetails;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/analytics/tenants")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemTenantAnalyticsController : ControllerBase
{
    private readonly ISystemTenantAnalyticsService _service;
    private readonly ILogger<SystemTenantAnalyticsController> _logger;

    public SystemTenantAnalyticsController(
        ISystemTenantAnalyticsService service,
        ILogger<SystemTenantAnalyticsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>[SystemAdmin] Lấy tổng quan tài chính của một tenant.</summary>
    [HttpGet("{tenantId:guid}/financial-summary")]
    [ProducesResponseType<SystemTenantFinancialSummaryResponseDto>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFinancialSummary(
        [FromRoute] Guid tenantId,
        [FromQuery] SystemAnalyticsPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetTenantFinancialSummaryAsync(
                tenantId,
                query,
                cancellationToken));
        }
        catch (SystemAnalyticsQueryValidationException exception)
        {
            var problem = SystemAnalyticsProblemDetailsFactory.Validation(
                HttpContext,
                exception.Errors);
            return StatusCode(problem.Status!.Value, problem);
        }
        catch (KeyNotFoundException)
        {
            var problem = SystemAnalyticsProblemDetailsFactory.NotFound(
                HttpContext,
                "The specified tenant does not exist.");
            return StatusCode(problem.Status!.Value, problem);
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
                "Unexpected error while generating a System Analytics tenant financial summary.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(
                HttpContext);
            return StatusCode(problem.Status!.Value, problem);
        }
    }
}
