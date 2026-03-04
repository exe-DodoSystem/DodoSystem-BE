using AutoMapper;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Mappings;

public class AttendanceMappingProfile : Profile
{
    public AttendanceMappingProfile()
    {
        CreateMap<Attendance, AttendanceDto>()
            .ForMember(d => d.EmployeeFullName,
                opt => opt.MapFrom(s => s.Employee != null ? s.Employee.FullName : string.Empty))
            .ForMember(d => d.DepartmentName,
                opt => opt.MapFrom(s => s.Employee != null && s.Employee.Department != null
                    ? s.Employee.Department.Name
                    : null));

        CreateMap<Attendance, CheckInResponseDto>()
            .ForMember(d => d.EmployeeFullName,
                opt => opt.MapFrom(s => s.Employee != null ? s.Employee.FullName : string.Empty));

        CreateMap<Attendance, CheckOutResponseDto>()
            .ForMember(d => d.EmployeeFullName,
                opt => opt.MapFrom(s => s.Employee != null ? s.Employee.FullName : string.Empty));

        CreateMap<TenantAttendanceSetting, AttendanceConfigResponseDto>();

        CreateMap<AttendanceConfigDto, TenantAttendanceSetting>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.TenantId, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.Tenant, opt => opt.Ignore());
    }
}
