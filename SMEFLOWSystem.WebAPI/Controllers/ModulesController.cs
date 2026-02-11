using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModulesController : ControllerBase
{
    private readonly IModuleService _moduleService;

    public ModulesController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<ModuleDto>>> GetActive()
    {
        var modules = await _moduleService.GetAllActiveAsync();
        return Ok(modules);
    }

    [HttpGet("all")]
    public async Task<ActionResult<List<ModuleDto>>> GetAll()
    {
        var modules = await _moduleService.GetAllAsync();
        return Ok(modules);
    }

    [HttpPost]
    public async Task<ActionResult<ModuleDto>> Create([FromBody] ModuleCreateDto dto)
    {
        try
        {
            var module = await _moduleService.CreateAsync(dto);
            return Ok(module);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

