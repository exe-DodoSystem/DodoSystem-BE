using SMEFLOWSystem.Application.DTOs.ModuleDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IModuleSubscriptionService
{
    Task<List<ModuleSubscriptionDto>> GetMyAllAsync();
    Task<ModuleSubscriptionDto?> GetMyByModuleIdAsync(int moduleId);
    Task<ModuleSubscriptionDto?> GetMyByModuleCodeAsync(string code);
    Task<bool> HasUsableModuleAsync(string code);
    Task<bool> CancelMyModuleSubscriptionAsync(int moduleId);
}
