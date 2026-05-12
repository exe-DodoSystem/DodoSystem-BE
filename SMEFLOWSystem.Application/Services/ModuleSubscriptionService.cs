using AutoMapper;
using ShareKernel.Common.Enum;
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
    private readonly IBillingOrderRepository _billingOrderRepo;
    private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;

    public ModuleSubscriptionService(
        IMapper mapper,
        ICurrentTenantService currentTenantService,
        IModuleRepository moduleRepo,
        IModuleSubscriptionRepository moduleSubscriptionRepo,
        IBillingOrderRepository billingOrderRepo,
        IBillingOrderModuleRepository billingOrderModuleRepo)
    {
        _mapper = mapper;
        _currentTenantService = currentTenantService;
        _moduleRepo = moduleRepo;
        _moduleSubscriptionRepo = moduleSubscriptionRepo;
        _billingOrderRepo = billingOrderRepo;
        _billingOrderModuleRepo = billingOrderModuleRepo;
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

    public async Task<bool> CancelMyModuleSubscriptionAsync(int moduleId)
    {
        var tenantId = GetTenantIdOrThrow();
        var sub = await _moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenantId, moduleId);
        
        if (sub == null) 
            throw new Exception("Không tìm thấy module này trong danh sách đăng ký.");

        sub.IsDeleted = true;
        sub.Status = StatusEnum.ModuleSuspended;
        await _moduleSubscriptionRepo.UpdateIgnoreTenantAsync(sub);

        var pendingOrders = await _billingOrderRepo.GetByTenantIdAsync(tenantId);
        foreach (var order in pendingOrders.Where(o => string.Equals(o.PaymentStatus, StatusEnum.PaymentPending, StringComparison.OrdinalIgnoreCase)))
        {
            var orderModules = await _billingOrderModuleRepo.GetByBillingOrderIdIgnoreTenantAsync(order.Id);
            if (orderModules.Any(m => m.ModuleId == moduleId))
            {
                order.Status = StatusEnum.OrderCancelled;
                order.PaymentStatus = StatusEnum.OrderCancelled;
                await _billingOrderRepo.UpdateIgnoreTenantAsync(order);
            }
        }

        return true;
    }

    private Guid GetTenantIdOrThrow()
    {
        var tenantId = _currentTenantService.TenantId;
        if (!tenantId.HasValue) throw new UnauthorizedAccessException("Tenant not resolved");
        return tenantId.Value;
    }

}
