using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/shifts")]
public class HrShiftsController : ControllerBase
{
    private readonly IShiftManagementService _service;

    public HrShiftsController(IShiftManagementService service)
    {
        _service = service;
    }

    /// <summary>[TenantAdmin, HRManager] Lấy danh sách ca làm việc</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ShiftDto>>> GetPaged([FromQuery] ShiftQueryDto query)
    {
        try
        {
            return Ok(await _service.GetPagedAsync(query));
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

    /// <summary>[TenantAdmin, HRManager] Xem chi tiết một ca làm việc</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftDto>> GetById([FromRoute] Guid id)
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

    /// <summary>[TenantAdmin, HRManager] Tạo ca làm việc</summary>
    [HttpPost]
    public async Task<ActionResult<ShiftDto>> Create([FromBody] ShiftCreateDto request)
    {
        try
        {
            return Ok(await _service.CreateAsync(request));
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

    /// <summary>[TenantAdmin, HRManager] Cập nhật ca làm việc</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShiftDto>> Update([FromRoute] Guid id, [FromBody] ShiftCreateDto request)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, request));
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

    /// <summary>[TenantAdmin, HRManager] Xóa ca làm việc</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
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
