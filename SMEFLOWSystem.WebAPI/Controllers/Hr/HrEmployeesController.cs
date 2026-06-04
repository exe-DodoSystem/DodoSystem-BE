using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/employees")]
public class HrEmployeesController : ControllerBase
{
    private readonly IHrEmployeeService _service;

    public HrEmployeesController(IHrEmployeeService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách nhân sự (có phân trang) trong phạm vi quyền hạn của user</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<EmployeeDto>>> GetPaged([FromQuery] EmployeeQueryDto query)
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

    /// <summary>Lấy thông tin chi tiết một nhân viên</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById([FromRoute] Guid id)
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

    /// <summary>[TenantAdmin, HRManager] Thêm nhân viên mới trực tiếp</summary>
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] EmployeeCreateDto request)
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

    /// <summary>[TenantAdmin, HRManager] Cập nhật thông tin nhân viên</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update([FromRoute] Guid id, [FromBody] EmployeeUpdateDto request)
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

    /// <summary>[TenantAdmin, HRManager] Xóa nhân viên</summary>
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lấy tất cả nhân viên trong một phòng ban (không phân trang) - Dành cho Manager để xem danh sách nhân viên trực thuộc
    /// </summary>
    [Authorize(Policy = PolicyNames.HrAccess)]
    [HttpGet("department/{departmentId:guid}")]
    public async Task<ActionResult<List<EmployeeDto>>> GetByDepartmentId([FromRoute] Guid departmentId)
    {
        try
        {
            var employees = await _service.GetAllByDepartmentId(departmentId);
            return Ok(employees);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }
}