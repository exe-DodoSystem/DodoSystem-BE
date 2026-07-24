using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Common;

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

    /// <summary>Lấy danh sách các module đang hoạt động trên hệ thống</summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<ModuleDto>>> GetActive()
    {
        var modules = await _moduleService.GetAllActiveAsync();
        return Ok(modules);
    }

    /// <summary>Lấy danh sách tất cả các module (Bao gồm cả ngưng hoạt động)</summary>
    [HttpGet("all")]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
    public async Task<ActionResult<List<ModuleDto>>> GetAll()
    {
        var modules = await _moduleService.GetAllAsync();
        return Ok(modules);
    }

    /// <summary>[SystemAdmin] Cập nhật tên, mô tả và giá module</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
    public async Task<ActionResult<ModuleDto>> Update(
        [FromRoute] int id,
        [FromBody] ModuleUpdateDto dto)
    {
        try
        {
            var module = await _moduleService.UpdateAsync(id, dto);
            if (module == null)
                return Problem(statusCode: 404, title: "Module not found");
            return Ok(module);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400, title: "Invalid module", detail: ex.Message);
        }
    }

    /// <summary>[SystemAdmin] Kích hoạt module</summary>
    [HttpPut("{id:int}/activate")]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
    public async Task<IActionResult> Activate([FromRoute] int id)
    {
        var success = await _moduleService.ActivateModuleAsync(id);
        if (!success)
            return Problem(statusCode: 404, title: "Module not found");

        return Ok(new { message = "Đã kích hoạt module thành công." });
    }

    /// <summary>[SystemAdmin] Tạo module mới vào hệ thống</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
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

    /// <summary>[SystemAdmin] Ngừng hoạt động 1 module</summary>
    [HttpPut("{id}/deactivate")]
    [Authorize(Policy = PolicyNames.SystemAdmin)]
    public async Task<IActionResult> Deactivate([FromRoute] int id)
    {
        var success = await _moduleService.DeactivateModuleAsync(id);
        if (!success)
            return NotFound(new { error = "Không tìm thấy module hoặc module đã bị xóa." });

        return Ok(new { message = "Đã ngừng hoạt động module thành công." });
    }
}

