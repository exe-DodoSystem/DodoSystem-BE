using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IInviteService
    {
        Task SendInviteAsync(Guid tenantId, string email, int roleId, Guid? departmentId, Guid? positionId, string message, Guid? invitedByUserId = null);
        Task<Invite> ValidateInviteTokenAsync(string token);
        Task CompleteOnboardingAsync(string token, string fullName, string password, string? phone);
    }
}
