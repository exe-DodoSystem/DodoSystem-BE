using SMEFLOWSystem.Application.DTOs.ModuleDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IBillingOrderModuleService
{
    Task<List<BillingOrderModuleDto>> GetMyByModuleIdAsync(int moduleId);
    Task<List<BillingOrderModuleDto>> GetMyByModuleCodeAsync(string code);
}
