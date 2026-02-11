using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IModuleService
{
    Task<ModuleDto> CreateAsync(ModuleCreateDto dto);
    Task<List<ModuleDto>> GetAllAsync();
    Task<List<ModuleDto>> GetAllActiveAsync();
}
