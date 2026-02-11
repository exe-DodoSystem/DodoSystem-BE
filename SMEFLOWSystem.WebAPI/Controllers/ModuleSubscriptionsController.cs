using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModuleSubscriptionsController : ControllerBase
{
    private readonly IModuleSubscriptionService _service;

    public ModuleSubscriptionsController(
        IModuleSubscriptionService service)
    {
        _service = service;
    }

    [HttpGet("me/all")]
    public async Task<ActionResult<List<ModuleSubscriptionDto>>> GetMyAll()
    {
        try
        {
            var subs = await _service.GetMyAllAsync();
            return Ok(subs);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("me/by-module-id/{moduleId:int}")]
    public async Task<ActionResult<ModuleSubscriptionDto>> GetMyByModuleId([FromRoute] int moduleId)
    {
        try
        {
            var sub = await _service.GetMyByModuleIdAsync(moduleId);
            if (sub == null) return NotFound();
            return Ok(sub);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("me/by-module-code/{code}")]
    public async Task<ActionResult<ModuleSubscriptionDto>> GetMyByModuleCode([FromRoute] string code)
    {
        try
        {
            var sub = await _service.GetMyByModuleCodeAsync(code);
            if (sub == null) return NotFound();
            return Ok(sub);
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
