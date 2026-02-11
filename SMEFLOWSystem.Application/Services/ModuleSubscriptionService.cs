using AutoMapper;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class ModuleSubscriptionService : IModuleSubscriptionService
{
    private readonly IMapper _mapper;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IModuleRepository _moduleRepo;
    private readonly IModuleSubscriptionRepository _moduleSubscriptionRepo;

    public ModuleSubscriptionService(
        IMapper mapper,
        ICurrentTenantService currentTenantService,
        IModuleRepository moduleRepo,
        IModuleSubscriptionRepository moduleSubscriptionRepo)
    {
        _mapper = mapper;
        _currentTenantService = currentTenantService;
        _moduleRepo = moduleRepo;
        _moduleSubscriptionRepo = moduleSubscriptionRepo;
    }

    public async Task<List<ModuleSubscriptionDto>> GetMyAllAsync()
    {
        var tenantId = GetTenantIdOrThrow();
        var subs = await _moduleSubscriptionRepo.GetByTenantIgnoreTenantAsync(tenantId);
        return _mapper.Map<List<ModuleSubscriptionDto>>(subs);
    }

    public async Task<ModuleSubscriptionDto?> GetMyByModuleIdAsync(int moduleId)
    {
        var tenantId = GetTenantIdOrThrow();
        var sub = await _moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId, moduleId);
        return sub == null ? null : _mapper.Map<ModuleSubscriptionDto>(sub);
    }

    public async Task<ModuleSubscriptionDto?> GetMyByModuleCodeAsync(string code)
    {
        var tenantId = GetTenantIdOrThrow();
        var module = await _moduleRepo.GetByCodeAsync(code);
        if (module == null) throw new KeyNotFoundException("Module not found");

        var sub = await _moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId, module.Id);
        return sub == null ? null : _mapper.Map<ModuleSubscriptionDto>(sub);
    }

    private Guid GetTenantIdOrThrow()
    {
        var tenantId = _currentTenantService.TenantId;
        if (!tenantId.HasValue) throw new UnauthorizedAccessException("Tenant not resolved");
        return tenantId.Value;
    }

}
