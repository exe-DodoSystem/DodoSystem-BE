using AutoMapper;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class BillingOrderModuleService : IBillingOrderModuleService
{
    private readonly IMapper _mapper;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IModuleRepository _moduleRepo;
    private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;

    public BillingOrderModuleService(
        IMapper mapper,
        ICurrentTenantService currentTenantService,
        IModuleRepository moduleRepo,
        IBillingOrderModuleRepository billingOrderModuleRepo)
    {
        _mapper = mapper;
        _currentTenantService = currentTenantService;
        _moduleRepo = moduleRepo;
        _billingOrderModuleRepo = billingOrderModuleRepo;
    }

    public async Task<List<BillingOrderModuleDto>> GetMyByModuleIdAsync(int moduleId)
    {
        var tenantId = GetTenantIdOrThrow();
        var lines = await _billingOrderModuleRepo.GetByTenantAndModuleAsync(tenantId, moduleId);
        return _mapper.Map<List<BillingOrderModuleDto>>(lines);
    }

    public async Task<List<BillingOrderModuleDto>> GetMyByModuleCodeAsync(string code)
    {
        var tenantId = GetTenantIdOrThrow();
        var module = await _moduleRepo.GetByCodeAsync(code);
        if (module == null) throw new KeyNotFoundException("Module not found");

        var lines = await _billingOrderModuleRepo.GetByTenantAndModuleAsync(tenantId, module.Id);
        return _mapper.Map<List<BillingOrderModuleDto>>(lines);
    }

    private Guid GetTenantIdOrThrow()
    {
        var tenantId = _currentTenantService.TenantId;
        if (!tenantId.HasValue) throw new UnauthorizedAccessException("Tenant not resolved");
        return tenantId.Value;
    }

}
