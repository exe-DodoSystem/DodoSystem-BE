using AutoMapper;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Services;

public class ModuleService : IModuleService
{
    private readonly IMapper _mapper;
    private readonly IModuleRepository _moduleRepository;

    public ModuleService(IMapper mapper, IModuleRepository moduleRepository)
    {
        _mapper = mapper;
        _moduleRepository = moduleRepository;
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
}
