using AutoMapper;
using SMEFLOWSystem.Application.DTOs.ModuleDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Mappings;

public class ModuleDtosMappingProfile : Profile
{
    public ModuleDtosMappingProfile()
    {
        CreateMap<Module, ModuleDto>();
        CreateMap<ModuleSubscription, ModuleSubscriptionDto>();
        CreateMap<BillingOrderModule, BillingOrderModuleDto>();
    }
}
