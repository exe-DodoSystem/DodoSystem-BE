using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.ProblemDetails;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/analytics")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemAnalyticsController : ControllerBase
{
    private readonly ISystemAnalyticsService _service;
    private readonly ILogger<SystemAnalyticsController> _logger;

    public SystemAnalyticsController(
        ISystemAnalyticsService service,
        ILogger<SystemAnalyticsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>[SystemAdmin] Lấy chuỗi doanh thu invoiced, collected, outstanding và estimated MRR.</summary>
    [HttpGet("revenue-series")]
    [ProducesResponseType<SystemRevenueSeriesResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRevenueSeries(
        [FromQuery] SystemRevenueSeriesQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetRevenueSeriesAsync(query, cancellationToken));
        }
        catch (SystemAnalyticsQueryValidationException exception)
        {
            var problem = SystemAnalyticsProblemDetailsFactory.Validation(
                HttpContext,
                exception.Errors);
            return StatusCode(problem.Status!.Value, problem);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while generating the System Analytics revenue series.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(HttpContext);
            return StatusCode(problem.Status!.Value, problem);
        }
    }

    /// <summary>[SystemAdmin] Lấy phân bổ doanh thu collected theo module, tenant hoặc cổng thanh toán.</summary>
    [HttpGet("revenue-breakdown")]
    [ProducesResponseType<SystemRevenueBreakdownResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRevenueBreakdown(
        [FromQuery] SystemRevenueBreakdownQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetRevenueBreakdownAsync(query, cancellationToken));
        }
        catch (SystemAnalyticsQueryValidationException exception)
        {
            var problem = SystemAnalyticsProblemDetailsFactory.Validation(
                HttpContext,
                exception.Errors);
            return StatusCode(problem.Status!.Value, problem);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while generating the System Analytics revenue breakdown.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(HttpContext);
            return StatusCode(problem.Status!.Value, problem);
        }
    }
}
