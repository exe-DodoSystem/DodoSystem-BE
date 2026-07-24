using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.WebAPI.Controllers.System;

[Route("api/system/tenants")]
[ApiController]
[Authorize(Policy = PolicyNames.SystemAdmin)]
public class SystemTenantsController : ControllerBase
{
    private readonly ISystemTenantService _systemTenantService;

    public SystemTenantsController(ISystemTenantService systemTenantService)
    {
        _systemTenantService = systemTenantService;
    }

    /// <summary>[SystemAdmin] Lấy danh sách tất cả các Tenant (Công ty) đang sử dụng hệ thống</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemTenantQueryDto request,
        CancellationToken cancellationToken)
    {
        var result = await _systemTenantService.GetAllAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy thông tin chi tiết một Tenant theo ID</summary>
    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _systemTenantService.GetByIdAsync(tenantId, cancellationToken);

        if (tenant == null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Tenant not found",
                detail: "Không tìm thấy tenant.");

        return Ok(tenant);
    }

    /// <summary>[SystemAdmin] Lấy danh sách user của tenant</summary>
    [HttpGet("{tenantId:guid}/users")]
    public async Task<IActionResult> GetUsers(
        [FromRoute] Guid tenantId,
        [FromQuery] SystemTenantUserQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _systemTenantService.GetUsersAsync(
            tenantId,
            query,
            cancellationToken);
        if (result == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Tenant not found",
                detail: "Không tìm thấy tenant.");
        }

        return Ok(result);
    }

    /// <summary>[SystemAdmin] Suspend hoặc reactivate tenant</summary>
    [HttpPatch("{tenantId:guid}/status")]
    public Task<IActionResult> ChangeStatus(
        [FromRoute] Guid tenantId,
        [FromBody] SystemTenantStatusUpdateDto request,
        CancellationToken cancellationToken)
        => ExecuteCommandAsync(
            () => _systemTenantService.ChangeStatusAsync(tenantId, request, cancellationToken));

    private async Task<IActionResult> ExecuteCommandAsync<T>(Func<Task<T>> command)
    {
        try
        {
            return Ok(await command());
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: 400, title: "Invalid tenant command", detail: exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException exception)
        {
            return Problem(statusCode: 404, title: "Tenant not found", detail: exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(statusCode: 409, title: "Invalid tenant status transition", detail: exception.Message);
        }
    }
}
