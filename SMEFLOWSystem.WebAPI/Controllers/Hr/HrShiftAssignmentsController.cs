using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/shift-assignments")]
public class HrShiftAssignmentsController : ControllerBase
{
    private readonly IShiftManagementService _service;

    public HrShiftAssignmentsController(IShiftManagementService service)
    {
        _service = service;
    }

    /// <summary>[TenantAdmin, HRManager, Manager] Gán lịch ca hàng loạt</summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<List<EmployeeShiftPatternDto>>> BulkAssign([FromBody] ShiftAssignmentBulkCreateDto request)
    {
        try
        {
            return Ok(await _service.BulkAssignPatternAsync(request));
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

    /// <summary>[TenantAdmin, HRManager, Manager] Xem danh sách gán lịch ca</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<EmployeeShiftPatternDto>>> GetPaged([FromQuery] ShiftAssignmentQueryDto query)
    {
        try
        {
            return Ok(await _service.GetAssignmentsPagedAsync(query));
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

    /// <summary>[TenantAdmin, HRManager, Manager] Xem chi tiết một bản ghi gán lịch ca</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeShiftPatternDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            return Ok(await _service.GetAssignmentByIdAsync(id));
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
}
