using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.Leave;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Common;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/leaves")]
public class LeaveRequestController : ControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveRequestController(ILeaveRequestService service)
    {
        _service = service;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được danh tính người dùng.");
        }
        return userId;
    }

    #region Employee Endpoints

    /// <summary>Nộp đơn xin nghỉ phép</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitLeaveRequestDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.SubmitLeaveRequestAsync(userId, dto);
            return Ok(new { Data = result, Message = "Nộp đơn xin nghỉ phép thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(401, new { Error = ex.Message });
        }
    }

    /// <summary>Hủy đơn xin nghỉ phép</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.CancelLeaveRequestAsync(userId, id);
            return Ok(new { Data = result, Message = "Hủy đơn xin nghỉ phép thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Lấy danh sách đơn xin nghỉ phép của tôi</summary>
    [HttpGet("my-requests")]
    public async Task<IActionResult> GetMyRequests()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.GetMyLeaveRequestsAsync(userId);
            return Ok(new { Data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Lấy số dư nghỉ phép của tôi</summary>
    [HttpGet("my-balances")]
    public async Task<IActionResult> GetMyBalances([FromQuery] int? year)
    {
        try
        {
            var userId = GetCurrentUserId();
            var targetYear = year ?? DateTime.UtcNow.Year;
            var result = await _service.GetMyBalancesAsync(userId, targetYear);
            return Ok(new { Data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion

    #region Manager/HR Endpoints

    /// <summary>Phê duyệt đơn xin nghỉ phép (Manager, HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.HrAccess)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve([FromRoute] Guid id, [FromBody] ApproveLeaveRequestDto dto)
    {
        try
        {
            var hrUserId = GetCurrentUserId();
            var result = await _service.ApproveLeaveRequestAsync(hrUserId, id, dto);
            return Ok(new { Data = result, Message = "Đã phê duyệt đơn xin nghỉ phép." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Từ chối đơn xin nghỉ phép (Manager, HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.HrAccess)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] RejectLeaveRequestDto dto)
    {
        try
        {
            var hrUserId = GetCurrentUserId();
            var result = await _service.RejectLeaveRequestAsync(hrUserId, id, dto);
            return Ok(new { Data = result, Message = "Đã từ chối đơn xin nghỉ phép." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Lấy danh sách đơn xin nghỉ phép đang chờ duyệt (Manager, HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.HrAccess)]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var result = await _service.GetPendingRequestsAsync();
        return Ok(new { Data = result });
    }

    /// <summary>Lấy tất cả đơn xin nghỉ phép (Manager, HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.HrAccess)]
    [HttpGet("all")]
    public async Task<IActionResult> GetAllRequests()
    {
        var result = await _service.GetAllRequestsAsync();
        return Ok(new { Data = result });
    }

    /// <summary>Lấy báo cáo số dư nghỉ phép của nhân viên (HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    [HttpGet("balances-report")]
    public async Task<IActionResult> GetBalancesReport([FromQuery] int? year)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var result = await _service.GetLeaveBalancesReportAsync(targetYear);
        return Ok(new { Data = result });
    }

    /// <summary>Cập nhật thủ công số dư nghỉ phép của nhân viên (HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    [HttpPut("balances/{id:guid}")]
    public async Task<IActionResult> UpdateBalance([FromRoute] Guid id, [FromBody] UpdateLeaveBalanceDto dto)
    {
        try
        {
            var result = await _service.UpdateLeaveBalanceAsync(id, dto);
            return Ok(new { Data = result, Message = "Cập nhật số dư nghỉ phép thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion

    #region Leave Type Master Data (HR, Admin)

    /// <summary>Lấy tất cả loại nghỉ phép</summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetLeaveTypes()
    {
        var result = await _service.GetLeaveTypesAsync();
        return Ok(new { Data = result });
    }

    /// <summary>Tạo loại nghỉ phép mới (HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    [HttpPost("types")]
    public async Task<IActionResult> CreateLeaveType([FromBody] CreateLeaveTypeDto dto)
    {
        try
        {
            var result = await _service.CreateLeaveTypeAsync(dto);
            return Ok(new { Data = result, Message = "Tạo loại nghỉ phép thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Cập nhật loại nghỉ phép (HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    [HttpPut("types/{id:guid}")]
    public async Task<IActionResult> UpdateLeaveType([FromRoute] Guid id, [FromBody] UpdateLeaveTypeDto dto)
    {
        try
        {
            var result = await _service.UpdateLeaveTypeAsync(id, dto);
            return Ok(new { Data = result, Message = "Cập nhật loại nghỉ phép thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Xóa loại nghỉ phép (HR, Admin)</summary>
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteLeaveType([FromRoute] Guid id)
    {
        try
        {
            await _service.DeleteLeaveTypeAsync(id);
            return Ok(new { Message = "Xóa loại nghỉ phép thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion
}
