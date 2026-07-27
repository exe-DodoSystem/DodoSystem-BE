using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/manual-timesheets")]
public class HrManualTimesheetsController : ControllerBase
{
    private readonly IManualTimesheetService _service;
    private readonly ICurrentTenantService _currentTenantService;

    public HrManualTimesheetsController(IManualTimesheetService service, ICurrentTenantService currentTenantService)
    {
        _service = service;
        _currentTenantService = currentTenantService;
    }

    /// <summary>[Admin, HR] Nhập hoặc cập nhật bảng công nhập tay</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> UpsertManualTimesheet([FromBody] ManualMonthlyTimesheetUpsertDto request)
    {
        var tenantId = RequireTenantId();
        var result = await _service.UpsertAsync(tenantId, request);
        return Ok(new { Data = result, Message = "Lưu bảng công nhập tay thành công." });
    }

    /// <summary>[Admin, HR, Manager] Lấy danh sách bảng công nhập tay theo tháng/năm</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.HrAccess)]
    public async Task<IActionResult> GetManualTimesheets([FromQuery] int month, [FromQuery] int year)
    {
        var tenantId = RequireTenantId();
        var result = await _service.GetByMonthAsync(tenantId, month, year);
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Xóa bảng công nhập tay theo ID</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> DeleteManualTimesheet(Guid id)
    {
        var tenantId = RequireTenantId();
        var success = await _service.DeleteAsync(tenantId, id);
        if (!success)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy bảng công hoặc không có quyền xóa.");
        }

        return Ok(new { Message = "Xóa bảng công nhập tay thành công." });
    }

    private Guid RequireTenantId()
    {
        return _currentTenantService.TenantId
            ?? throw new UnauthorizedAccessException(
                "Không tìm thấy tenant.");
    }
}
