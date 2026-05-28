using AutoMapper;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Mappings
{
    public class PayrollMappingProfile : Profile
    {
        public PayrollMappingProfile() 
        {
            CreateMap<Payroll, PayrollDto>()
                .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee != null ? src.Employee.FullName : string.Empty))
                .ForMember(dest => dest.EmployeeCode, opt => opt.Ignore()) // Tạm ignore vì bảng Employee hiện chưa có Entity Code, nếu sau này có thì đổi lại
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => (src.Employee != null && src.Employee.Department != null) ? src.Employee.Department.Name : string.Empty));

            CreateMap<UpdatePayrollDto, Payroll>()
                .ForMember(dest => dest.CustomBonus, opt => opt.Condition(src => src.CustomBonus.HasValue))
                .ForMember(dest => dest.CustomDeduction, opt => opt.Condition(src => src.CustomDeduction.HasValue))
                // Gán Reason từ Dto vào Notes của Payroll
                .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Reason));
        }
    }
}
