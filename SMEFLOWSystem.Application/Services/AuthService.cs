using AutoMapper;
using Hangfire;
using Microsoft.Extensions.Configuration;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.AuthDtos;
using SMEFLOWSystem.Application.DTOs.UserDtos;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IUserRoleRepository _userRoleRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IModuleRepository _moduleRepo;
        private readonly IModuleSubscriptionRepository _moduleSubscriptionRepo;
        private readonly ITransaction _transaction;
        private readonly IConfiguration _config;
        private readonly IBillingOrderService _billingOrderService;
        private readonly IBillingService _billingService;
        private readonly IMapper _mapper;

        // Constructor Injection
        public AuthService(
            ITenantRepository tenantRepo,
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IUserRoleRepository userRoleRepo,
            ICustomerRepository customerRepo,
            IModuleRepository moduleRepo,
            IModuleSubscriptionRepository moduleSubscriptionRepo,
            ITransaction transaction,
            IConfiguration config,
            IBillingOrderService billingOrderService,
            IBillingService billingService,
            IMapper mapper)
        {
            _tenantRepo = tenantRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
            _customerRepo = customerRepo;
            _moduleRepo = moduleRepo;
            _moduleSubscriptionRepo = moduleSubscriptionRepo;
            _transaction = transaction;
            _config = config;
            _billingOrderService = billingOrderService;
            _billingService = billingService;
            _mapper = mapper;
        }

        public async Task<bool> RegisterTenantAsync(RegisterRequestDto request)
        {
            var existingUser = await _userRepo.GetUserByEmailAsync(request.AdminEmail);
            if (existingUser != null)
                throw new Exception("Email này đã được sử dụng!");

            if (request.ModuleIds == null || request.ModuleIds.Length == 0)
                throw new Exception("Vui lòng chọn ít nhất 1 module!");

            Guid createdOrderId = Guid.Empty;
            string adminEmail = request.AdminEmail;
            string companyName = request.CompanyName;
            await _transaction.ExecuteAsync(async () =>
            {
                var now = DateTime.UtcNow;
                var trialEnd = now.AddDays(14);

                // TẠO TENANT (CÔNG TY)
                var newTenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.CompanyName,
                    Status = StatusEnum.TenantTrial,
                    SubscriptionEndDate = DateOnly.FromDateTime(trialEnd),
                    CreatedAt = now,
                };

                await _tenantRepo.AddAsync(newTenant);


                // TẠO USER ADMIN CHO TENANT ĐÓ
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = newTenant.Id,
                    FullName = request.AdminFullName,
                    Email = request.AdminEmail,
                    Phone = request.PhoneNumber ?? string.Empty,
                    PasswordHash = AuthHelper.HashPassword(request.Password),
                    IsActive = true,
                    IsVerified = false,
                    CreatedAt = now
                };

                await _userRepo.AddAsync(adminUser);


                // UPDATE OWNER CHO TENANT
                newTenant.OwnerUserId = adminUser.Id;
                await _tenantRepo.UpdateAsync(newTenant);


                // GÁN QUYỀN (ROLE) ADMIN CHO USER
                var adminRole = await _roleRepo.GetRoleByNameAsync("TenantAdmin");

                if (adminRole == null)
                    throw new Exception("Lỗi hệ thống: Không tìm thấy Role 'TenantAdmin'. Hãy chạy Seed Data trước.");

                var userRole = new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                };

                await _userRoleRepo.AddUserRoleAsync(userRole);


                // TẠO KHÁCH HÀNG ĐẠI DIỆN (INTERNAL CUSTOMER)
                var internalCustomer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TenantId = newTenant.Id,
                    Name = request.CompanyName,
                    Email = request.AdminEmail,
                    Type = "Internal",
                    CreatedAt = now
                };

                await _customerRepo.AddAsync(internalCustomer);

                // TẠO MODULE SUBSCRIPTIONS (TRIAL)
                var modules = await _moduleRepo.GetByIdsAsync(request.ModuleIds);
                if (modules.Count != request.ModuleIds.Distinct().Count())
                    throw new Exception("Có module không tồn tại hoặc đang bị tắt!");

                foreach (var module in modules)
                {
                    var sub = new ModuleSubscription
                    {
                        Id = Guid.NewGuid(),
                        TenantId = newTenant.Id,
                        ModuleId = module.Id,
                        StartDate = now,
                        EndDate = trialEnd,
                        Status = StatusEnum.ModuleTrial,
                        CreatedAt = now,
                        IsDeleted = false
                    };
                    await _moduleSubscriptionRepo.AddAsync(sub);
                }

                // TẠO ĐƠN THANH TOÁN DỊCH VỤ (BILLING ORDER) - optional pay early
                var newOrder = await _billingOrderService.CreateModuleBillingOrderAsync(
                    newTenant.Id,
                    internalCustomer.Id,
                    request.ModuleIds,
                    isTrialOrder: false);

                createdOrderId = newOrder.Id;
            });

            if (createdOrderId != Guid.Empty)
            {
                await _billingService.EnqueuePaymentLinkEmailAsync(createdOrderId, adminEmail, companyName);
            }

            return true;
        }

        public async Task<LoginUserDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepo.GetUserByEmailAsync(request.Email);

            if (user == null)
                throw new Exception("Tài khoản hoặc mật khẩu không chính xác");

            if (!AuthHelper.VerifyPassword(request.Password, user.PasswordHash))
                throw new Exception("Tài khoản hoặc mật khẩu không chính xác");

            if (!user.IsActive)
                throw new Exception("Tài khoản của bạn đã bị khóa.");

            var tenant = user.Tenant;
            if (tenant == null) throw new Exception("Không tìm thấy tenant");

            // Expiry check: if tenant has end date and it's expired, suspend + block login
            if (tenant.SubscriptionEndDate.HasValue)
            {
                var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
                if (tenant.SubscriptionEndDate.Value < todayUtc)
                {
                    tenant.Status = StatusEnum.TenantSuspended;
                    await _tenantRepo.UpdateAsync(tenant);

                    throw new Exception("Hết hạn trial, thanh toán để tiếp tục");
                }
            }

            if (string.Equals(tenant.Status, StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Hết hạn trial, thanh toán để tiếp tục");

            if (!string.Equals(tenant.Status, StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tenant.Status, StatusEnum.TenantTrial, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Tài khoản công ty chưa sẵn sàng để đăng nhập.");

            var token = AuthHelper.GenerateJwtToken(user, _config);
            var userDto =  _mapper.Map<LoginUserDto>(user);
            userDto.Token = token;

            return userDto;
        }
    }
}
