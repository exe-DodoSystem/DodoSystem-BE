using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.WebAPI.Helpers;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceController(IAttendanceService service)
    {
        _service = service;
    }

    [HttpPost("checkin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CheckInResponseDto>> CheckIn(
        [FromForm] double latitude,
        [FromForm] double longitude,
        IFormFile? selfie)
    {
        try
        {
            var request = new CheckInRequestDto
            {
                Latitude = latitude,
                Longitude = longitude,
                SelfieBase64 = await FormFileHelper.ToBase64DataUriAsync(selfie)
            };
            return Ok(await _service.CheckInAsync(request));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("checkout")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CheckOutResponseDto>> CheckOut(
        [FromForm] double latitude,
        [FromForm] double longitude,
        IFormFile? selfie)
    {
        try
        {
            var request = new CheckOutRequestDto
            {
                Latitude = latitude,
                Longitude = longitude,
                SelfieBase64 = await FormFileHelper.ToBase64DataUriAsync(selfie)
            };
            return Ok(await _service.CheckOutAsync(request));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-status")]
    public async Task<ActionResult<AttendanceStatusDto>> GetMyStatus([FromQuery] DateOnly? date)
    {
        try
        {
            var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            return Ok(await _service.GetMyStatusAsync(d));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

        [HttpGet("today")]
    public async Task<ActionResult<TodayAttendanceDto?>> GetTodayStatus()
    {
        try
        {
            var result = await _service.GetTodayStatusAsync();
            return Ok(result); // null nếu chưa check-in là bình thường
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    [HttpGet("my-history")]
    public async Task<ActionResult<List<AttendanceDto>>> GetMyHistory(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _service.GetMyHistoryAsync(from, to));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AttendanceDto>>> GetPaged([FromQuery] AttendanceQueryDto query)
    {
        try
        {
            return Ok(await _service.GetPagedAsync(query));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttendanceDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<ActionResult<AttendanceDto>> Approve(
        [FromRoute] Guid id, [FromBody] AttendanceApproveDto dto)
    {
        try
        {
            return Ok(await _service.ApproveAsync(id, dto));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}
