using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/billing-orders")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemBillingOrdersController : ControllerBase
{
    private readonly ISystemBillingService _service;

    public SystemBillingOrdersController(ISystemBillingService service)
    {
        _service = service;
    }

    /// <summary>[SystemAdmin] Lấy danh sách hóa đơn toàn hệ thống</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemBillingOrderQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _service.GetBillingOrdersAsync(query, cancellationToken));

    /// <summary>[SystemAdmin] Lấy chi tiết hóa đơn và giao dịch thanh toán</summary>
    [HttpGet("{billingOrderId:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid billingOrderId,
        CancellationToken cancellationToken)
    {
        var detail = await _service.GetBillingOrderAsync(
            billingOrderId,
            cancellationToken);
        if (detail == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Billing order not found");
        }

        return Ok(detail);
    }
}
