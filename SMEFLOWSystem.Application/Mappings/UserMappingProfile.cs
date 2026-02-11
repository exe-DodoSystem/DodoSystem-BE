using AutoMapper;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Mappings
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            // Map từ User sang UserDto
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.TenantName,
                           opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : string.Empty));

            CreateMap<User, LoginUserDto>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
                .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : string.Empty))
                // Token thường được sinh ra sau khi login, không lấy trực tiếp từ entity
                .ForMember(dest => dest.Token, opt => opt.Ignore());



            CreateMap<UserDto, User>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) 
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) 
                .ForMember(dest => dest.Tenant, opt => opt.Ignore()) 
                .ForMember(dest => dest.UserRoles, opt => opt.Ignore()) 
                .ForMember(dest => dest.Employees, opt => opt.Ignore()); 


            CreateMap<UserUpdatedDto, User>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())              
                .ForMember(dest => dest.Email, opt => opt.Ignore())           
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())    
                .ForMember(dest => dest.TenantId, opt => opt.Ignore())        
                .ForMember(dest => dest.Tenant, opt => opt.Ignore())          
                .ForMember(dest => dest.UserRoles, opt => opt.Ignore())       
                .ForMember(dest => dest.Employees, opt => opt.Ignore())      
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())      
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));

            CreateMap<LoginUserDto, User>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())              
                .ForMember(dest => dest.Email, opt => opt.Ignore())           
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())    
                .ForMember(dest => dest.TenantId, opt => opt.Ignore())        
                .ForMember(dest => dest.Tenant, opt => opt.Ignore())          
                .ForMember(dest => dest.UserRoles, opt => opt.Ignore())       
                .ForMember(dest => dest.Employees, opt => opt.Ignore())       
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())       
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));


        }

    }
}
