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
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { Error = "User is not authenticated correctly." });
        }

        try
        {
            var result = await _service.SubmitPunchAsync(userId, request);
            return Ok(new { Data = result, Message = "Punch submitted successfully" });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { Error = "Employee not found for current user." });
        }
    }

    // Rewrite other methods later...
}
