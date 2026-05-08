using AutoMapper;
using SMEFLOWSystem.Application.DTOs.NotificationDtos;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Mappings
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<Notification, NotificationDto>();
        }
    }
}
