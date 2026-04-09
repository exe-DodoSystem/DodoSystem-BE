using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance/setting")]
public class AttendanceSettingController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceSettingController(IAttendanceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AttendanceConfigResponseDto>> GetConfig()
    {
        try
        {
            return Ok(await _service.GetConfigAsync());
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    [HttpPut]
    public async Task<ActionResult<AttendanceConfigResponseDto>> UpsertConfig([FromBody] AttendanceConfigDto dto)
    {
        try
        {
            return Ok(await _service.UpsertConfigAsync(dto));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
