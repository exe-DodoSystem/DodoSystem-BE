using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BillingOrderModulesController : ControllerBase
{
    private readonly IBillingOrderModuleService _service;

    public BillingOrderModulesController(
        IBillingOrderModuleService service)
    {
        _service = service;
    }
    /// <summary>[TenantAdmin] Lấy thông tin các module đã mua thuộc 1 module ID cụ thể</summary>

    [Authorize(Policy = PolicyNames.TenantAdmin)]
    [HttpGet("me/by-module-id/{moduleId:int}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetMyByModuleId([FromRoute] int moduleId)
    {
        try
        {
            var lines = await _service.GetMyByModuleIdAsync(moduleId);
            return Ok(lines);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
    /// <summary>[TenantAdmin] Lấy thông tin các module đã mua thuộc 1 module Code cụ thể</summary>

    [Authorize(Policy = PolicyNames.TenantAdmin)]
    [HttpGet("me/by-module-code/{code}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetMyByModuleCode([FromRoute] string code)
    {
        try
        {
            var lines = await _service.GetMyByModuleCodeAsync(code);
            return Ok(lines);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
    /// <summary>[TenantAdmin] Lấy danh sách module thuộc 1 đơn hàng thanh toán (Billing Order) cụ thể</summary>

    [Authorize(Policy = PolicyNames.TenantAdmin)]
    [HttpGet("me/by-billing-order-id/{billingOrderId:guid}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetByBillingOrderId([FromRoute] Guid billingOrderId)
    {
        var lines = await _service.GetByBillingOrderIdAsync(billingOrderId);
        return Ok(lines);
    }
    /// <summary>[SystemAdmin] Lấy danh sách module thuộc 1 đơn hàng (Bỏ qua filter Tenant)</summary>

    [Authorize(Policy = PolicyNames.SystemAdmin)]
    [HttpGet("by-billing-order-id-ignore-tenant/{billingOrderId:guid}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetByBillingOrderIdIgnoreTenant([FromRoute] Guid billingOrderId)
    {
        var lines = await _service.GetByBillingOrderIdIgnoreTenantAsync(billingOrderId);
        return Ok(lines);
    }
}
