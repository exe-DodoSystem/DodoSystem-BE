using AutoMapper;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;
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

        // Shift Mappings
        CreateMap<Shift, ShiftDto>();
        CreateMap<ShiftSegment, ShiftSegmentDto>();
        
        CreateMap<ShiftCreateDto, Shift>()
            .ForMember(d => d.Segments, opt => opt.MapFrom(s => s.Segments));
        CreateMap<ShiftSegmentCreateDto, ShiftSegment>();

        // Shift Pattern Mappings
        CreateMap<ShiftPattern, ShiftPatternDto>();
        CreateMap<ShiftPatternDay, ShiftPatternDayDto>();
        
        CreateMap<ShiftPatternCreateDto, ShiftPattern>()
            .ForMember(d => d.Days, opt => opt.MapFrom(s => s.Days));
        CreateMap<DayCreateDto, ShiftPatternDay>();

        // Employee Shift Pattern Mappings
        CreateMap<EmployeeShiftPattern, EmployeeShiftPatternDto>()
            .ForMember(d => d.EmployeeName, opt => opt.MapFrom(s => s.Employee != null ? s.Employee.FullName : string.Empty))
            .ForMember(d => d.EmployeeDepartment, opt => opt.MapFrom(s => s.Employee != null && s.Employee.Department != null ? s.Employee.Department.Name : string.Empty))
            .ForMember(d => d.ShiftPatternName, opt => opt.MapFrom(s => s.ShiftPattern != null ? s.ShiftPattern.Name : string.Empty));

        // Manual Monthly Timesheet Mappings
        CreateMap<ManualMonthlyTimesheet, ManualMonthlyTimesheetDto>()
            .ForMember(d => d.EmployeeName, opt => opt.MapFrom(s => s.Employee != null ? s.Employee.FullName : string.Empty));
        CreateMap<ManualMonthlyTimesheetUpsertDto, ManualMonthlyTimesheet>();
    }
}
