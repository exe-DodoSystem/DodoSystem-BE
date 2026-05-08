using AutoMapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SharedKernel.DTOs;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.RoleDtos;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ICloudinaryService _cloudinary;
        private readonly IConfiguration _configuration;
        private readonly IOutboxMessageRepository _outboxMessageRepository;

        public UserService(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IUserRoleRepository userRoleRepository,
            IMapper mapper,
            IEmailService emailService,
            ICloudinaryService cloudinary,
            IConfiguration configuration,
            IOutboxMessageRepository outboxMessageRepository)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _userRoleRepository = userRoleRepository;
            _mapper = mapper;
            _emailService = emailService;
            _cloudinary = cloudinary;
            _configuration = configuration;
            _outboxMessageRepository = outboxMessageRepository;
        }

        public async Task<UserDto> CreateAsync(UserCreatedDto user, Guid tenantId)
        {
            var userEntity = await _userRepository.IsEmailExistAsync(user.Email);
            if (userEntity)
               throw new ArgumentException($"User with email {user.Email} is already existed");

            var newUser = _mapper.Map<User>(user);
            newUser.TenantId = tenantId;
            await _userRepository.AddAsync(newUser);

            return _mapper.Map<UserDto>(newUser);
        }

        public async Task<IEnumerable<UserDto>> GetAllUserAsync()
        {
            var user = await _userRepository.GetAllUsersAsync();
            return _mapper.Map<IEnumerable<UserDto>>(user);
        }

        public async Task<PagedResultDto<UserDto>> GetAllUsersPagingAsync(PagingRequestDto request)
        {
            var (items, totalCount) = await _userRepository.GetAllUserPagingAsync(request.PageNumber, request.PageSize);
            
            var userDto =  _mapper.Map<IEnumerable<UserDto>>(items);
            return new PagedResultDto<UserDto>
            {
                Items = userDto,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if(user == null)
            {
                throw new ArgumentException($"User with email {email} is not existed");
            }
            return user;
        }

        public async Task<UserDto> GetUserByIdAsync(Guid id)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                throw new ArgumentException($"User with id {id} is not existed");
            }
            return _mapper.Map<UserDto>(user);
        }

        public async Task<IEnumerable<UserDto>> GetUserByNameAsync(string name)
        { 
            var users = await _userRepository.GetUserByNameAsync(name);
            if(users == null)
            {
                throw new ArgumentException($"User with name {name} is not existed");
            }
            return _mapper.Map<IEnumerable<UserDto>>(users);
        }

        public async Task<UserDto> InvitedUserAsync(UserCreatedDto user, Guid tenantId)
        {
            var userEntity = await _userRepository.IsEmailExistAsync(user.Email);
            if (userEntity)
                throw new ArgumentException($"User with email {user.Email} is already existed");

            if (user.RoleId <= 0)
                throw new ArgumentException("RoleId không hợp lệ");

            var role = await _roleRepository.GetRoleByIdAsync(user.RoleId);
            if (role == null)
                throw new ArgumentException("Role không tồn tại");

            if (string.Equals(role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Không thể gán role SystemAdmin bằng invite tenant");

            var newUser = _mapper.Map<User>(user);
            newUser.TenantId = tenantId;
            newUser.PasswordHash = AuthHelper.HashPassword(user.PasswordHash);
            newUser.IsActive = true;
            newUser.IsVerified = true;
            newUser.CreatedAt = DateTime.UtcNow;
            await _userRepository.AddAsync(newUser);

            var userRole = new UserRole
            {
                UserId = newUser.Id,
                TenantId = tenantId,
                RoleId = role.Id
            };
            await _userRoleRepository.AddUserRoleAsync(userRole);

            var emailEvent = new EmailNotificationRequestedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                TenantId = tenantId,
                ToEmail = newUser.Email,
                Subject = "Lời mời kích hoạt tài khoản DodoSystem",
                Body = $"<h3>Chúc mừng {newUser.FullName}!</h3><p>Tài khoản của bạn đã được đăng ký.</p><p>Bạn có thể đăng nhập ngay bây giờ.</p>",
                CorrelationId = newUser.Id.ToString()
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

            return _mapper.Map<UserDto>(newUser);
        }

        public async Task<UserDto> UpdateAsync(Guid id, UserUpdatedDto user)
        {
            var existingUser = await _userRepository.GetUserByIdAsync(id);
            if (existingUser == null)
            {
                throw new ArgumentException($"User with id {id} is not existed");
            }
            var userEntity = _mapper.Map<User>(user);
            userEntity.Id = existingUser.Id;

            var updatedUser = await _userRepository.UpdateUserAsync(userEntity);
            return _mapper.Map<UserDto>(updatedUser);

        }

        public async Task SetUserRoleAsync(Guid userId, List<int> roleIds)
        {
            if (roleIds == null || roleIds.Count == 0)
                throw new ArgumentException("RoleIds không được rỗng");

            var distinctRoleIds = roleIds.Where(r => r > 0).Distinct().ToList();
            if (distinctRoleIds.Count == 0)
                throw new ArgumentException("RoleIds không hợp lệ");

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("Không tìm thấy user");

            var roles = await _roleRepository.GetByIdsAsync(distinctRoleIds);
            if (roles.Count != distinctRoleIds.Count)
            {
                var missing = distinctRoleIds.Except(roles.Select(r => r.Id)).ToList();
                throw new ArgumentException($"Role không tồn tại: {string.Join(", ", missing)}");
            }

            if (roles.Any(r => string.Equals(r.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Không thể gán role SystemAdmin");

            await _userRoleRepository.ReplaceUserRolesAsync(user.Id, user.TenantId, roles.Select(r => r.Id));
        }

        public async Task UpdatePasswordAsync(Guid id, string password)
        {
            var existingUser = await _userRepository.UpdatePasswordIgnoreTenantAsync(id, password);
            if (existingUser == null)
            {
                throw new ArgumentException($"User with id {id} is not existed");
            }
        }

        public async Task<UserDto> UpdateAvatarAsync(Guid userId, Stream imageStream, string fileName)
        {
            var user = await _userRepository.GetUserByIdAsync(userId)
                ?? throw new ArgumentException($"User with id {userId} is not existed");


            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                try { await _cloudinary.DeleteAsync(user.AvatarUrl); } catch { }
            }

            var avatarUrl = await _cloudinary.UploadFileAsync(imageStream, fileName, "avatars");

            user.AvatarUrl = avatarUrl;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateUserAsync(user);

            return _mapper.Map<UserDto>(user);
        }
    }
}
