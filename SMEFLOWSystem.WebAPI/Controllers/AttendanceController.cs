using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;

    public AttendanceController(IAttendanceService service)
    {
        _service = service;
    }

    [HttpPost("submit-punch")]
    public async Task<IActionResult> SubmitPunch([FromBody] SubmitPunchRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var employeeId))
        {
            return Unauthorized(new { Error = "User is not authenticated correctly." });
        }

        var result = await _service.SubmitPunchAsync(employeeId, request);
        return Ok(new { Data = result, Message = "Punch submitted successfully" });
    }

    // Rewrite other methods later...
}
