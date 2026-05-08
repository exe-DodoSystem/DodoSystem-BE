using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance/setting")]
public class AttendanceSettingController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceSettingController(IAttendanceService service)
    {
        _service = service;
    }

    /// <summary>Lấy cấu hình chấm công hiện tại của Tenant</summary>
    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        try
        {
            var result = await _service.GetSettingsAsync();
            return Ok(new { Data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>[Admin, HR] Cập nhật cấu hình chấm công (tạo mới nếu chưa có)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpsertConfig([FromBody] UpdateAttendanceSettingRequestDto dto)
    {
        try
        {
            var result = await _service.UpdateSettingsAsync(dto);
            return Ok(new { Data = result, Message = "Cập nhật cấu hình chấm công thành công." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
