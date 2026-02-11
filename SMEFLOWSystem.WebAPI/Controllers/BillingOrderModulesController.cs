using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BillingOrderModulesController : ControllerBase
{
    private readonly IBillingOrderModuleService _service;

    public BillingOrderModulesController(
        IBillingOrderModuleService service)
    {
        _service = service;
    }

    [HttpGet("me/by-module-id/{moduleId:int}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetMyByModuleId([FromRoute] int moduleId)
    {
        try
        {
            var lines = await _service.GetMyByModuleIdAsync(moduleId);
            return Ok(lines);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("me/by-module-code/{code}")]
    public async Task<ActionResult<List<BillingOrderModuleDto>>> GetMyByModuleCode([FromRoute] string code)
    {
        try
        {
            var lines = await _service.GetMyByModuleCodeAsync(code);
            return Ok(lines);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
