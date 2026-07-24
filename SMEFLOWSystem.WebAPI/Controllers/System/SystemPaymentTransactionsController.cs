using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/payment-transactions")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public sealed class SystemPaymentTransactionsController : ControllerBase
{
    private readonly ISystemBillingService _service;

    public SystemPaymentTransactionsController(ISystemBillingService service)
    {
        _service = service;
    }

    /// <summary>[SystemAdmin] Lấy danh sách giao dịch thanh toán toàn hệ thống</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemPaymentTransactionQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPaymentTransactionsAsync(query, cancellationToken));
}
