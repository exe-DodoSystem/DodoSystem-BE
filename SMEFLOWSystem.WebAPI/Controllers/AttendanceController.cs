using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.WebAPI.Helpers;


using SMEFLOWSystem.WebAPI.Filters;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance")]
[RequireModule("ATTENDANCE")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceController(IAttendanceService service)
    {
        _service = service;
    }

    /// <summary>Gửi yêu cầu chấm công (Check-in/Check-out)</summary>
    [HttpPost("submit-punch")]
    public async Task<IActionResult> SubmitPunch([FromBody] SubmitPunchRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { Error = "User is not authenticated correctly." });
        }

        var result = await _service.SubmitPunchAsync(userId, request);
        return Ok(new { Data = result, Message = "Punch submitted successfully" });
    }

    /// <summary>Gửi yêu cầu chấm công (multipart/form-data) kèm ảnh selfie</summary>
    [HttpPost("submit-punch-form")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitPunchForm([FromForm] SubmitPunchRequestDto request, IFormFile? selfie)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { Error = "User is not authenticated correctly." });
        }

        request.SelfieBase64 ??=
            await FormFileHelper.ToBase64DataUriAsync(selfie);
        var result = await _service.SubmitPunchAsync(userId, request);
        return Ok(new { Data = result, Message = "Punch submitted successfully" });
    }

    /// <summary>Lấy thông tin chấm công hôm nay của user đăng nhập</summary>
    [HttpGet("my-today")]
    public async Task<IActionResult> GetMyTodayStatus()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _service.GetMyTodayStatusAsync(userId);
        return Ok(new { Data = result });
    }

    /// <summary>Lấy lịch sử chấm công của user đăng nhập theo tháng/năm</summary>
    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyHistory([FromQuery] int month, [FromQuery] int year)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _service.GetMyHistoryAsync(userId, month, year);
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Chấm công bằng tay cho nhân viên</summary>
    [HttpPost("manual-punch")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> ManualPunch([FromBody] ManualPunchRequestDto request)
    {
        var result = await _service.ManualPunchAsync(request);
        return Ok(new { Data = result, Message = "Chấm công bằng tay thành công (HR Manual Punch)." });
    }

    /// <summary>[Admin, HR] Tính toán lại công cho nhân viên trong 1 khoảng thời gian</summary>
    [HttpPost("recalculate/{employeeId}")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> RecalculateAttendance(Guid employeeId, [FromQuery] string fromDate, [FromQuery] string toDate)
    {
        if (!DateOnly.TryParse(fromDate, out var from) ||
            !DateOnly.TryParse(toDate, out var to))
        {
            throw new BusinessRuleException(
                "Ngày bắt đầu hoặc ngày kết thúc không hợp lệ.",
                "ATTENDANCE_INVALID_DATE_RANGE");
        }

        if (from > to)
        {
            throw new BusinessRuleException(
                "Từ ngày không thể lớn hơn Đến ngày",
                "ATTENDANCE_INVALID_DATE_RANGE");
        }

        await _service.RecalculateAttendanceAsync(employeeId, from, to);
        return Ok(new { Message = $"Đã phát lệnh chạy lại Engine từ ngày {from} đến ngày {to}. Kết quả sẽ có sau ít phút." });
    }

    /// <summary>Gửi yêu cầu giải trình công (Appeal)</summary>
    [HttpPost("appeals")]
    public async Task<IActionResult> SubmitAppeal([FromBody] SubmitAppealRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _service.SubmitAppealAsync(userId, request);
        return Ok(new { Data = result, Message = "Đã gửi yêu cầu giải trình công thành công." });
    }

    /// <summary>Lấy danh sách các yêu cầu giải trình của user đăng nhập</summary>
    [HttpGet("appeals")]
    public async Task<IActionResult> GetMyAppeals()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _service.GetMyAppealsAsync(userId);
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Lấy danh sách các yêu cầu giải trình đang chờ duyệt</summary>
    [HttpGet("appeals/pending")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> GetPendingAppeals()
    {
        var result = await _service.GetPendingAppealsAsync();
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Xử lý (Duyệt/Từ chối) yêu cầu giải trình</summary>
    [HttpPut("appeals/{appealId}/process")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> ProcessAppeal(Guid appealId, [FromBody] ApproveAppealRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var hrUserId))
            return Unauthorized();

        var result = await _service.ProcessAppealAsync(hrUserId, appealId, request);
        var statusStr = request.IsApproved ? "Duyệt" : "Từ chối";
        return Ok(new { Data = result, Message = $"Đã {statusStr} yêu cầu giải trình thành công." });
    }

    /// <summary>[Admin, HR] Lấy báo cáo chấm công tháng của tất cả nhân viên</summary>
    [HttpGet("hr-monthly-report")]
    [Authorize(Policy = PolicyNames.HrAccess)]
    public async Task<IActionResult> GetHRMonthlyReport([FromQuery] int month, [FromQuery] int year)
    {
        var result = await _service.GetHRMonthlyReportAsync(month, year);
        return Ok(new { Data = result });
    }
}
