using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

/// <summary>
/// API quản lý phân quyền phòng ban cho Manager.
/// Chỉ TenantAdmin mới được gọi các endpoint Assign/Unassign/Replace.
/// Manager và HRManager có thể xem phạm vi quản lý của chính họ.
/// </summary>
[ApiController]
[Authorize]
[Route("api/hr/managers")]
public class ManagerDepartmentsController : ControllerBase
{
    private readonly IManagerDepartmentService _service;

    public ManagerDepartmentsController(IManagerDepartmentService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách phòng ban mà Manager đang được quản lý</summary>
    [HttpGet("{userId:guid}/departments")]
    public async Task<ActionResult<List<ManagerDepartmentDto>>> GetByManager([FromRoute] Guid userId)
    {
        try
        {
            return Ok(await _service.GetByManagerAsync(userId));
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

    /// <summary>
    /// [TenantAdmin] Gán Manager vào 1 hoặc nhiều phòng ban.
    /// Bỏ qua các phòng ban đã được gán trước đó.
    /// </summary>
    [HttpPost("{userId:guid}/departments")]
    public async Task<IActionResult> Assign([FromRoute] Guid userId, [FromBody] AssignManagerDepartmentDto request)
    {
        try
        {
            await _service.AssignAsync(userId, request);
            return Ok(new { success = true, message = "Gán phòng ban thành công." });
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

    /// <summary>[TenantAdmin] Gỡ quyền Manager khỏi 1 phòng ban cụ thể</summary>
    [HttpDelete("{userId:guid}/departments/{departmentId:guid}")]
    public async Task<IActionResult> Unassign([FromRoute] Guid userId, [FromRoute] Guid departmentId)
    {
        try
        {
            await _service.UnassignAsync(userId, departmentId);
            return Ok(new { success = true, message = "Gỡ quyền phòng ban thành công." });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Bạn không có quyền truy cập" });
        }
    }

    /// <summary>
    /// [TenantAdmin] Thay thế toàn bộ danh sách phòng ban của Manager.
    /// Xóa tất cả phân công cũ và thay bằng danh sách mới.
    /// </summary>
    [HttpPut("{userId:guid}/departments")]
    public async Task<IActionResult> Replace([FromRoute] Guid userId, [FromBody] AssignManagerDepartmentDto request)
    {
        try
        {
            await _service.ReplaceAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật danh sách phòng ban thành công." });
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
}
