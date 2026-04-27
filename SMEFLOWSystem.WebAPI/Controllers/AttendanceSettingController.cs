using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance/setting")]
public class AttendanceSettingController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceSettingController(IAttendanceService service)
    {
        _service = service;
    }

    // Uncomment and rewrite later
    // [HttpGet]
    // public async Task<IActionResult> GetConfig()
    // {
    // }

    // [HttpPost]
    // public async Task<IActionResult> UpsertConfig([FromBody] AttendanceConfigRequestDto dto)
    // {
    // }
}
