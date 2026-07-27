using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemAnalyticsDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.Exceptions;
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
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
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
            return ApiProblemDetailsFactory.CreateResult(problem);
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
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
    }

    /// <summary>[SystemAdmin] Lấy phân bổ doanh thu collected theo module, tenant hoặc cổng thanh toán.</summary>
    [HttpGet("revenue-breakdown")]
    [ProducesResponseType<SystemRevenueBreakdownResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
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
            return ApiProblemDetailsFactory.CreateResult(problem);
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
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
    }

    /// <summary>[SystemAdmin] Lấy các hành động cần xử lý từ dữ liệu vận hành hiện có.</summary>
    [HttpGet("action-center")]
    [ProducesResponseType<SystemActionCenterResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActionCenter(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetActionCenterAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while generating the System Analytics action center.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(HttpContext);
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
    }

    /// <summary>[SystemAdmin] Dự báo collected revenue theo tháng bằng linear trend.</summary>
    [HttpGet("revenue-forecast")]
    [ProducesResponseType<SystemRevenueForecastResponseDto>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblemDetails>(
        StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ApiProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRevenueForecast(
        [FromQuery] SystemRevenueForecastQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetRevenueForecastAsync(
                query,
                cancellationToken));
        }
        catch (SystemAnalyticsQueryValidationException exception)
        {
            var problem = SystemAnalyticsProblemDetailsFactory.Validation(
                HttpContext,
                exception.Errors);
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
        catch (InsufficientForecastHistoryException exception)
        {
            var problem =
                SystemAnalyticsProblemDetailsFactory.InsufficientForecastHistory(
                    HttpContext,
                    exception.Message);
            return ApiProblemDetailsFactory.CreateResult(problem);
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
                "Unexpected error while generating the System Analytics revenue forecast.");
            var problem = SystemAnalyticsProblemDetailsFactory.UnexpectedError(
                HttpContext);
            return ApiProblemDetailsFactory.CreateResult(problem);
        }
    }
}
