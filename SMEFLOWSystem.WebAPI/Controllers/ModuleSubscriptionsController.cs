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

    /// <summary>Lấy danh sách tất cả các gói đăng ký module của Tenant hiện tại</summary>
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

    /// <summary>Lấy thông tin gói đăng ký của Tenant theo Module ID</summary>
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

    /// <summary>Lấy thông tin gói đăng ký của Tenant theo Module Code</summary>
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
