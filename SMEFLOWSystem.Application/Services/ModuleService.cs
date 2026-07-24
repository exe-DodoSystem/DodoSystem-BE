using AutoMapper;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Logging;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Services;

public class ModuleService : IModuleService
{
    private readonly IMapper _mapper;
    private readonly IModuleRepository _moduleRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<ModuleService>? _logger;

    public ModuleService(
        IMapper mapper,
        IModuleRepository moduleRepository,
        ICurrentUserService? currentUserService = null,
        ILogger<ModuleService>? logger = null)
    {
        _mapper = mapper;
        _moduleRepository = moduleRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ModuleDto> CreateAsync(ModuleCreateDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var module = new Module
        {
            Code = (dto.Code?.Trim() ?? string.Empty).ToUpperInvariant(),
            ShortCode = (dto.ShortCode?.Trim() ?? string.Empty).ToUpperInvariant(),
            Name = dto.Name?.Trim() ?? string.Empty,
            Description = dto.Description?.Trim() ?? string.Empty,
            MonthlyPrice = dto.MonthlyPrice,
            IsActive = dto.IsActive
        };

        if (string.IsNullOrWhiteSpace(module.Code)) throw new ArgumentException("Code is required");
        if (string.IsNullOrWhiteSpace(module.ShortCode)) throw new ArgumentException("ShortCode is required");
        if (string.IsNullOrWhiteSpace(module.Name)) throw new ArgumentException("Name is required");
        if (module.MonthlyPrice < 0) throw new ArgumentException("MonthlyPrice must be >= 0");

        var isDuplicated = await _moduleRepository.ExistsByCodeOrShortCodeAsync(module.Code, module.ShortCode);
        if (isDuplicated)
        {
            throw new ArgumentException("Code hoặc ShortCode đã tồn tại");
        }

        await _moduleRepository.AddAsync(module);
        return _mapper.Map<ModuleDto>(module);
    }

    public async Task<List<ModuleDto>> GetAllAsync()
    {
        var modules = await _moduleRepository.GetAllAsync();
        return _mapper.Map<List<ModuleDto>>(modules);
    }

    public async Task<List<ModuleDto>> GetAllActiveAsync()
    {
        var modules = await _moduleRepository.GetAllActiveAsync();
        return _mapper.Map<List<ModuleDto>>(modules);
    }

    public async Task<ModuleDto?> UpdateAsync(int moduleId, ModuleUpdateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(dto));
        if (dto.MonthlyPrice < 0)
            throw new ArgumentException("MonthlyPrice must be greater than or equal to 0.", nameof(dto));

        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module == null)
            return null;

        var description = dto.Description?.Trim() ?? string.Empty;
        if (string.Equals(module.Name, name, StringComparison.Ordinal)
            && string.Equals(module.Description, description, StringComparison.Ordinal)
            && module.MonthlyPrice == dto.MonthlyPrice)
            return _mapper.Map<ModuleDto>(module);

        var beforeName = module.Name;
        var beforeDescription = module.Description;
        var beforePrice = module.MonthlyPrice;
        module.Name = name;
        module.Description = description;
        module.MonthlyPrice = dto.MonthlyPrice;
        module.UpdatedAt = DateTime.UtcNow;
        await _moduleRepository.UpdateAsync(module);
        _logger?.LogWarning(
            SystemAdminLogEvents.ModuleUpdated,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; ModuleId={ModuleId}; BeforeName={BeforeName}; AfterName={AfterName}; BeforeDescription={BeforeDescription}; AfterDescription={AfterDescription}; BeforeMonthlyPrice={BeforeMonthlyPrice}; AfterMonthlyPrice={AfterMonthlyPrice}",
            "MODULE_UPDATED",
            _currentUserService?.UserId,
            module.Id,
            beforeName,
            module.Name,
            beforeDescription,
            module.Description,
            beforePrice,
            module.MonthlyPrice);
        return _mapper.Map<ModuleDto>(module);
    }

    public async Task<bool> ActivateModuleAsync(int moduleId)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module == null)
            return false;

        if (!module.IsActive)
        {
            module.IsActive = true;
            module.UpdatedAt = DateTime.UtcNow;
            await _moduleRepository.UpdateAsync(module);
            LogModuleStateChange(module, "MODULE_ACTIVATED");
        }
        return true;
    }

    public async Task<bool> DeactivateModuleAsync(int moduleId)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module == null)
            return false;

        if (module.IsActive)
        {
            module.IsActive = false;
            module.UpdatedAt = DateTime.UtcNow;
            await _moduleRepository.UpdateAsync(module);
            LogModuleStateChange(module, "MODULE_DEACTIVATED");
        }
        return true;
    }

    private void LogModuleStateChange(Module module, string action)
    {
        _logger?.LogWarning(
            SystemAdminLogEvents.ModuleUpdated,
            "SystemAdmin action {Action}; ActorUserId={ActorUserId}; ModuleId={ModuleId}; IsActive={IsActive}",
            action,
            _currentUserService?.UserId,
            module.Id,
            module.IsActive);
    }
}
