using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SharedKernel.DTOs;
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
    public async Task<IActionResult> GetAll([FromQuery] PagingRequestDto request)
    {
        var result = await _systemTenantService.GetAllAsync(request);
        return Ok(result);
    }

    /// <summary>[SystemAdmin] Lấy thông tin chi tiết một Tenant theo ID</summary>
    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid tenantId)
    {
        var tenant = await _systemTenantService.GetByIdAsync(tenantId);

        if (tenant == null)
            return NotFound(new { error = "Không tìm thấy tenant" });

        return Ok(tenant);
    }
}
