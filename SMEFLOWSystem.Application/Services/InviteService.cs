using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class InviteService : IInviteService
    {
        private readonly IInviteRepository _inviteRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IEmailService _emailService;
        private readonly IModuleRepository _moduleRepository;
        private readonly IModuleSubscriptionRepository _moduleSubscriptionRepository;
        private readonly IConfiguration _configuration;
        private readonly IOutboxMessageRepository _outboxMessageRepository;

        public InviteService(
            IInviteRepository inviteRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IEmployeeRepository employeeRepository,
            IRoleRepository roleRepository,
            IEmailService emailService,
            IModuleRepository moduleRepository,
            IModuleSubscriptionRepository moduleSubscriptionRepository,
            IConfiguration configuration,
            IOutboxMessageRepository outboxMessageRepository)
        {
            _inviteRepository = inviteRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _employeeRepository = employeeRepository;
            _roleRepository = roleRepository;
            _emailService = emailService;
            _moduleRepository = moduleRepository;
            _moduleSubscriptionRepository = moduleSubscriptionRepository;
            _configuration = configuration;
            _outboxMessageRepository = outboxMessageRepository;
        }

        public Task CompleteOnboardingAsync(string token, string fullName, string password, string? phone)
        {
            return CompleteOnboardingInternalAsync(token, fullName, password, phone);
        }

        public Task SendInviteAsync(Guid tenantId, string email, int roleId, Guid? departmentId, Guid? positionId, string message)
        {
            return SendInviteInternalAsync(tenantId, email, roleId, departmentId, positionId, message);
        }

        public Task<Invite> ValidateInviteTokenAsync(string token)
        {
            return ValidateTokenInternalAsync(token);
        }

        private async Task SendInviteInternalAsync(Guid tenantId, string email, int roleId, Guid? departmentId, Guid? positionId, string message)
        {
            var emailExists = await _userRepository.IsEmailExistAsync(email);
            if (emailExists)
                throw new ArgumentException("Email này đã được sử dụng!");

            var role = await _roleRepository.GetRoleByIdAsync(roleId);
            if (role == null)
                throw new ArgumentException("Role không tồn tại");

            if (departmentId.HasValue != positionId.HasValue)
                throw new ArgumentException("DepartmentId và PositionId phải đi cùng nhau");

            var token = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;

            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = email,
                RoleId = roleId,
                DepartmentId = departmentId,
                PositionId = positionId,
                Token = token,
                ExpiryDate = now.AddDays(7),
                IsUsed = false,
                Message = message,
                CreatedAt = now,
                UpdatedAt = null,
                IsDeleted = false
            };

            await _inviteRepository.AddInviteAsync(invite);

            var onboardingUrl = _configuration["Invite:OnboardingUrl"];
            var tokenText = string.IsNullOrWhiteSpace(onboardingUrl)
                ? token
                : $"{onboardingUrl.TrimEnd('/')}/{token}";
            var emailEvent = new EmailNotificationRequestedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                TenantId = tenantId,
                ToEmail = email,
                Subject = $"Lời mời tham gia SMEFLOW System",
                Body = $"<p>Bạn được mời tham gia hệ thống.</p><p>Mã/Link onboarding: <strong>{tokenText}</strong></p><p>{message}</p>",
                CorrelationId = invite.Id.ToString()
            };

            var exchange = _configuration["RabbitMQ:Exchange"] ?? "smeflow.exchange";
            var routingKey = _configuration["RabbitMQ:RoutingKeys:SendEmail"] ?? "email.send";

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventId = emailEvent.EventId,
                EventType = nameof(EmailNotificationRequestedEvent),
                Exchange = exchange,
                RoutingKey = routingKey,
                Payload = JsonConvert.SerializeObject(emailEvent),
                CorrelationId = emailEvent.CorrelationId,
                Status = StatusEnum.OutboxPending,
                OccurredOnUtc = DateTime.UtcNow,
                NextAttemptOnUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            
            await _outboxMessageRepository.AddAsync(outboxMessage);
        }

        private async Task<Invite> ValidateTokenInternalAsync(string token)
        {
            var invite = await _inviteRepository.GetInviteByTokenAsync(token);
            if (invite == null)
                throw new ArgumentException("Token không hợp lệ hoặc đã hết hạn");
            return invite;
        }

        private async Task CompleteOnboardingInternalAsync(string token, string fullName, string password, string? phone)
        {
            var invite = await _inviteRepository.GetInviteByTokenAsync(token);
            if (invite == null)
                throw new ArgumentException("Token không hợp lệ hoặc đã hết hạn");

            // Enforce HR module subscription for the tenant
            var module = await _moduleRepository.GetByCodeAsync("HR");
            if (module == null)
                throw new InvalidOperationException("Module 'HR' chưa được cấu hình");

            var sub = await _moduleSubscriptionRepository.GetByTenantAndModuleIgnoreTenantAsync(invite.TenantId, module.Id);
            var now = DateTime.UtcNow;
            var validStatus = sub != null
                              && (string.Equals(sub.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(sub.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
                              && sub.EndDate > now;
            if (!validStatus)
                throw new UnauthorizedAccessException("Bạn chưa đăng ký module HR");

            var emailExists = await _userRepository.IsEmailExistAsync(invite.Email);
            if (emailExists)
                throw new ArgumentException("Email này đã được sử dụng!");

            var createdAt = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = invite.TenantId,
                FullName = fullName,
                Email = invite.Email,
                Phone = phone ?? string.Empty,
                PasswordHash = AuthHelper.HashPassword(password),
                IsActive = true,
                IsVerified = true,
                CreatedAt = createdAt
            };
            await _userRepository.AddAsync(user);

            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = invite.RoleId,
                TenantId = invite.TenantId
            };
            await _userRoleRepository.AddUserRoleAsync(userRole);

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                TenantId = invite.TenantId,
                UserId = user.Id,
                DepartmentId = invite.DepartmentId,
                PositionId = invite.PositionId,
                FullName = fullName,
                Phone = phone ?? string.Empty,
                Email = invite.Email,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ResignationDate = null,
                BaseSalary = 0,
                Status = StatusEnum.EmployeeWorking,
                CreatedAt = createdAt,
                UpdatedAt = null,
                IsDeleted = false
            };
            await _employeeRepository.AddAsync(employee);

            invite.IsUsed = true;
            invite.UpdatedAt = DateTime.UtcNow;
            await _inviteRepository.UpdateInviteAsync(invite);
        }
    }
}
