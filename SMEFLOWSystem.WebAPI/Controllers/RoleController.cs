using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.SharedKernel.Common;

using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.RoleDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }
        /// <summary>Lấy danh sách tất cả Roles</summary>

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }
        /// <summary>Lấy thông tin Role theo ID</summary>

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleById(int id)
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }
            return Ok(role);
        }
        /// <summary>[SystemAdmin] Tạo Role mới</summary>

        [Authorize(Policy = PolicyNames.SystemAdmin)]
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] RoleUpdatedDto roleDto)
        {
            try
            {
                await _roleService.AddRoleAsync(roleDto);
                return Ok(new { message = "Create role succcessfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        /// <summary>[SystemAdmin] Cập nhật thông tin Role</summary>

        [Authorize(Policy = PolicyNames.SystemAdmin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] RoleUpdatedDto roleDto)
        {
            try
            {
                var updatedRole = await _roleService.UpdateRoleAsync(id, roleDto);
                if (updatedRole == null)
                {
                    return NotFound();
                }
                return Ok(updatedRole);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        /// <summary>Lấy danh sách Roles có phân trang</summary>

        [Authorize]
        [HttpGet("all/page")]
        public async Task<IActionResult> GetAllByPage([FromQuery] PagingRequestDto request)
        {
            var pagedRoles = await _roleService.GetAllRolesPagingAsync(request);
            return Ok(pagedRoles);
        }

    }
}
