using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/subscriptions")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemSubscriptionsController : ControllerBase
{
    private readonly ISystemSubscriptionService _service;

    public SystemSubscriptionsController(ISystemSubscriptionService service)
    {
        _service = service;
    }

    /// <summary>[SystemAdmin] Lấy danh sách subscription toàn hệ thống</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemSubscriptionQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(query, cancellationToken));

    /// <summary>[SystemAdmin] Gia hạn subscription</summary>
    [HttpPost("{subscriptionId:guid}/extend")]
    public Task<IActionResult> Extend(
        [FromRoute] Guid subscriptionId,
        [FromBody] SystemSubscriptionExtendRequestDto request,
        CancellationToken cancellationToken)
        => ExecuteCommandAsync(
            () => _service.ExtendAsync(subscriptionId, request, cancellationToken));

    /// <summary>[SystemAdmin] Tạm ngưng subscription mà không hủy</summary>
    [HttpPost("{subscriptionId:guid}/suspend")]
    public Task<IActionResult> Suspend(
        [FromRoute] Guid subscriptionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SystemSubscriptionReasonRequestDto? request,
        CancellationToken cancellationToken)
        => ExecuteCommandAsync(
            () => _service.SuspendAsync(
                subscriptionId,
                request ?? new SystemSubscriptionReasonRequestDto(),
                cancellationToken));

    /// <summary>[SystemAdmin] Kích hoạt lại subscription đang tạm ngưng</summary>
    [HttpPost("{subscriptionId:guid}/reactivate")]
    public Task<IActionResult> Reactivate(
        [FromRoute] Guid subscriptionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SystemSubscriptionReasonRequestDto? request,
        CancellationToken cancellationToken)
        => ExecuteCommandAsync(
            () => _service.ReactivateAsync(
                subscriptionId,
                request ?? new SystemSubscriptionReasonRequestDto(),
                cancellationToken));

    private async Task<IActionResult> ExecuteCommandAsync<T>(Func<Task<T>> command)
    {
        try
        {
            return Ok(await command());
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: 400, title: "Invalid subscription command", detail: exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException exception)
        {
            return Problem(statusCode: 404, title: "Subscription not found", detail: exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(statusCode: 409, title: "Invalid subscription transition", detail: exception.Message);
        }
    }
}
