using AutoMapper;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Mappings;

public class HrMappingProfile : Profile
{
    public HrMappingProfile()
    {
        CreateMap<Department, DepartmentDto>();
        CreateMap<DepartmentCreateDto, Department>();
        CreateMap<DepartmentUpdateDto, Department>();

        CreateMap<Position, PositionDto>();
        CreateMap<PositionCreateDto, Position>();
        CreateMap<PositionUpdateDto, Position>();

        CreateMap<Employee, EmployeeDto>()
            .ForMember(d => d.DepartmentName, opt => opt.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty))
            .ForMember(d => d.PositionName, opt => opt.MapFrom(s => s.Position != null ? s.Position.Name : string.Empty))
            .ForMember(d => d.RoleName, opt => opt.MapFrom(s =>
                s.User != null && s.User.UserRoles != null
                    ? s.User.UserRoles.Select(ur => ur.Role != null ? ur.Role.Name : string.Empty).FirstOrDefault() ?? string.Empty
                    : string.Empty));
        CreateMap<EmployeeCreateDto, Employee>();
        CreateMap<EmployeeUpdateDto, Employee>();
    }
}
