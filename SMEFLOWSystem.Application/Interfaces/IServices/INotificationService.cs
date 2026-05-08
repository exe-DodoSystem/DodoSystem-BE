using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.NotificationDtos;
using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface INotificationService
    {
        Task<PagedResultDto<NotificationDto>> GetMyNotificationsAsync(bool? isRead, int pageNumber, int pageSize);
        Task<int> GetUnreadCountAsync();
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllAsReadAsync();
        Task CreateAsync(Guid tenantId, Guid recipientUserId, string title, string message, string type, Guid? referenceId = null);
    }
}
