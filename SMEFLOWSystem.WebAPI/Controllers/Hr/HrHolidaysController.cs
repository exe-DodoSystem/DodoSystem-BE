using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers.Hr;

[ApiController]
[Authorize]
[Route("api/hr/holidays")]
public class HrHolidaysController : ControllerBase
{
    private readonly IAttendanceService _service;

    public HrHolidaysController(IAttendanceService service)
    {
        _service = service;
    }

    /// <summary>[Admin, HR] Thêm ngày lễ mới</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> CreatePublicHoliday([FromBody] CreatePublicHolidayDto request)
    {
        var result = await _service.CreatePublicHolidayAsync(request);
        return Ok(new { Data = result, Message = "Tạo ngày nghỉ lễ thành công." });
    }

    /// <summary>[Admin, HR] Lấy danh sách các ngày nghỉ lễ</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.HrAccess)]
    public async Task<IActionResult> GetPublicHolidays()
    {
        var result = await _service.GetPublicHolidaysAsync();
        return Ok(new { Data = result });
    }

    /// <summary>[Admin, HR] Xóa ngày nghỉ lễ theo ID</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PolicyNames.AdminOrHr)]
    public async Task<IActionResult> DeletePublicHoliday(Guid id)
    {
        await _service.DeletePublicHolidayAsync(id);
        return Ok(new { Message = "Xóa ngày nghỉ lễ thành công." });
    }
}
