using SMEFLOWSystem.Application.DTOs.ModuleDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IModuleSubscriptionService
{
    Task<List<ModuleSubscriptionDto>> GetMyAllAsync();
    Task<ModuleSubscriptionDto?> GetMyByModuleIdAsync(int moduleId);
    Task<ModuleSubscriptionDto?> GetMyByModuleCodeAsync(string code);
}
