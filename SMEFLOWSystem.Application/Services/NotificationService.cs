using AutoMapper;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.NotificationDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepo;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public NotificationService(
            INotificationRepository notificationRepo,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _notificationRepo = notificationRepo;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<NotificationDto>> GetMyNotificationsAsync(bool? isRead, int pageNumber, int pageSize)
        {
            var userId = _currentUser.RequireUserId();

            var (items, total) = await _notificationRepo.GetByUserIdPagedAsync(userId, isRead, pageNumber, pageSize);

            return new PagedResultDto<NotificationDto>
            {
                Items = _mapper.Map<List<NotificationDto>>(items),
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<int> GetUnreadCountAsync()
        {
            var userId = _currentUser.RequireUserId();
            return await _notificationRepo.GetUnreadCountAsync(userId);
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            var userId = _currentUser.RequireUserId();

            var notification = await _notificationRepo.GetByIdAsync(notificationId)
                ?? throw new KeyNotFoundException("Không tìm thấy thông báo.");

            if (notification.RecipientUserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên thông báo này.");

            await _notificationRepo.MarkAsReadAsync(notificationId);
        }

        public async Task MarkAllAsReadAsync()
        {
            var userId = _currentUser.RequireUserId();
            await _notificationRepo.MarkAllAsReadAsync(userId);
        }

        public async Task CreateAsync(Guid tenantId, Guid recipientUserId, string title, string message, string type, Guid? referenceId = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RecipientUserId = recipientUserId,
                Title = title,
                Message = message,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepo.AddAsync(notification);
        }
    }
}
