using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/departments")]
public class HrDepartmentsController : ControllerBase
{
    private readonly IHrDepartmentService _service;

    public HrDepartmentsController(IHrDepartmentService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách các phòng ban mà user có quyền truy cập</summary>
    [HttpGet]
    public async Task<ActionResult<List<DepartmentDto>>> GetAccessible()
    {
        try
        {
            return Ok(await _service.GetAccessibleAsync());
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    /// <summary>[TenantAdmin, HRManager] Tạo mới phòng ban</summary>
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] DepartmentCreateDto request)
    {
        try
        {
            var created = await _service.CreateAsync(request);
            return Ok(created);
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

    /// <summary>[TenantAdmin, HRManager] Cập nhật thông tin phòng ban</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update([FromRoute] Guid id, [FromBody] DepartmentUpdateDto request)
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

    /// <summary>[TenantAdmin, HRManager] Xóa phòng ban</summary>
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
