using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/shift-patterns")]
public class HrShiftPatternsController : ControllerBase
{
    private readonly IShiftManagementService _service;

    public HrShiftPatternsController(IShiftManagementService service)
    {
        _service = service;
    }

    /// <summary>[TenantAdmin, HRManager] Lấy danh sách lịch ca</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ShiftPatternDto>>> GetPaged([FromQuery] ShiftPatternQueryDto query)
    {
        try
        {
            return Ok(await _service.GetPatternsPagedAsync(query));
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

    /// <summary>[TenantAdmin, HRManager] Xem chi tiết 1 lịch ca</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftPatternDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            return Ok(await _service.GetPatternByIdAsync(id));
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

    /// <summary>[TenantAdmin, HRManager] Tạo lịch ca</summary>
    [HttpPost]
    public async Task<ActionResult<ShiftPatternDto>> Create([FromBody] ShiftPatternCreateDto request)
    {
        try
        {
            return Ok(await _service.CreatePatternAsync(request));
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

    /// <summary>[TenantAdmin, HRManager] Cập nhật lịch ca</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShiftPatternDto>> Update([FromRoute] Guid id, [FromBody] ShiftPatternCreateDto request)
    {
        try
        {
            return Ok(await _service.UpdatePatternAsync(id, request));
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
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>[TenantAdmin, HRManager] Xóa lịch ca</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        try
        {
            await _service.DeletePatternAsync(id);
            return Ok(new { success = true });
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
            return Conflict(new { error = ex.Message });
        }
    }
}
