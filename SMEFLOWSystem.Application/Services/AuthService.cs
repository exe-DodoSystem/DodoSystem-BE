using Hangfire;
using Microsoft.Extensions.Configuration;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.AuthDtos;
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
        private readonly ITransaction _transaction;
        private readonly IConfiguration _config;
        private readonly IBillingOrderService _billingOrderService;
        private readonly IBillingService _billingService;

        // Constructor Injection
        public AuthService(
            ITenantRepository tenantRepo,
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IUserRoleRepository userRoleRepo,
            ICustomerRepository customerRepo,
            ITransaction transaction,
            IConfiguration config,
            IBillingOrderService billingOrderService,
            IBillingService billingService)
        {
            _tenantRepo = tenantRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
            _customerRepo = customerRepo;
            _transaction = transaction;
            _config = config;
            _billingOrderService = billingOrderService;
            _billingService = billingService;
        }

        public async Task<bool> RegisterTenantAsync(RegisterRequestDto request)
        {
            var existingUser = await _userRepo.GetUserByEmailAsync(request.AdminEmail);
            if (existingUser != null)
                throw new Exception("Email này đã được sử dụng!");

            Guid createdOrderId = Guid.Empty;
            string adminEmail = request.AdminEmail;
            string companyName = request.CompanyName;
            await _transaction.ExecuteAsync(async () =>
            {
                // TẠO TENANT (CÔNG TY)
                var newTenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.CompanyName,
                    Status = StatusEnum.TenantPending,
                    SubscriptionPlanId = request.SubscriptionPlanId,
                    CreatedAt = DateTime.UtcNow,
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
                    CreatedAt = DateTime.UtcNow
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
                    CreatedAt = DateTime.UtcNow
                };

                await _customerRepo.AddAsync(internalCustomer);

                // TẠO ĐƠN THANH TOÁN DỊCH VỤ (BILLING ORDER)
                var newOrder = await _billingOrderService.CreateSubscriptionBillingOrderAsync(
                    newTenant.Id,
                    internalCustomer.Id,
                    request.SubscriptionPlanId);
                createdOrderId = newOrder.Id;
            });

            if (createdOrderId != Guid.Empty)
            {
                await _billingService.EnqueuePaymentLinkEmailAsync(createdOrderId, adminEmail, companyName);
            }

            return true;
        }

        public async Task<string> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepo.GetUserByEmailAsync(request.Email);

            if (user == null)
                throw new Exception("Tài khoản hoặc mật khẩu không chính xác");

            if (!AuthHelper.VerifyPassword(request.Password, user.PasswordHash))
                throw new Exception("Tài khoản hoặc mật khẩu không chính xác");

            if (!user.IsActive)
                throw new Exception("Tài khoản của bạn đã bị khóa.");

            if (user.Tenant.Status == StatusEnum.TenantSuspended)
                throw new Exception("Dịch vụ của công ty đã hết hạn/bị treo.");

            if (user.Tenant.Status == StatusEnum.TenantPending)
                throw new Exception("Công ty chưa kích hoạt dịch vụ (Chưa thanh toán).");

            var token = AuthHelper.GenerateJwtToken(user, _config);
            return token;
        }
    }
}
