using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/positions")]
public class HrPositionsController : ControllerBase
{
    private readonly IHrPositionService _service;

    public HrPositionsController(IHrPositionService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách chức vụ thuộc một phòng ban cụ thể</summary>
    [HttpGet]
    public async Task<ActionResult<List<PositionDto>>> GetByDepartment([FromQuery] Guid departmentId)
    {
        try
        {
            return Ok(await _service.GetByDepartmentAsync(departmentId));
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

    /// <summary>[TenantAdmin, HRManager] Tạo mới chức vụ</summary>
    [HttpPost]
    public async Task<ActionResult<PositionDto>> Create([FromBody] PositionCreateDto request)
    {
        try
        {
            return Ok(await _service.CreateAsync(request));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
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

    /// <summary>[TenantAdmin, HRManager] Cập nhật thông tin chức vụ</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PositionDto>> Update([FromRoute] Guid id, [FromBody] PositionUpdateDto request)
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>[TenantAdmin, HRManager] Xóa chức vụ</summary>
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
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
