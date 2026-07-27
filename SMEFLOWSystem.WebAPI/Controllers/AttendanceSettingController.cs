using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SMEFLOWSystem.Application.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System;
using System.Threading.Tasks;

using SMEFLOWSystem.WebAPI.Filters;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance/setting")]
[RequireModule("ATTENDANCE")]
public class AttendanceSettingController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceSettingController(IAttendanceService service)
    {
        _service = service;
    }

    /// <summary>Lấy cấu hình chấm công hiện tại của Tenant</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.HrAccess)]
    public async Task<IActionResult> GetConfig()
    {
        var result = await _service.GetSettingsAsync();
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Cập nhật cấu hình chấm công (tạo mới nếu chưa có)</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.HrAccess)]
    public async Task<IActionResult> UpsertConfig([FromBody] UpdateAttendanceSettingRequestDto dto)
    {
        var result = await _service.UpdateSettingsAsync(dto);
        return Ok(new { Data = result, Message = "Cập nhật cấu hình chấm công thành công." });
    }
}
