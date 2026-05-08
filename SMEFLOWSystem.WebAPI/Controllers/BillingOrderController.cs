using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Interfaces.IServices.System;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System.Security.Claims;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BillingOrderController : ControllerBase
    {
        private readonly IBillingOrderService _billingOrderService;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ISystemTenantService _systemTenantService;
        private readonly IBillingService _billingService;

        public BillingOrderController(
            IBillingOrderService billingOrderService, 
            ICurrentTenantService currentTenant, 
            ISystemTenantService systemTenantService,
            IBillingService billingService)
        {
            _billingOrderService = billingOrderService;
            _currentTenant = currentTenant;
            _systemTenantService = systemTenantService;
            _billingService = billingService;
        }

        /// <summary>Lấy danh sách các hóa đơn của Tenant hiện tại</summary>
        [HttpGet("me/billing-orders")]
        public async Task<IActionResult> GetBillingOrdersByTenantAsync()
        {
            var tenantId = _currentTenant.TenantId;
            if (!tenantId.HasValue)
            {
                return NotFound();
            }
            var orders = await _billingOrderService.GetBillingOrdersAsync(tenantId.Value);
            return Ok(orders);
        }

        /// <summary>
        /// Mua thêm Module cho công ty
        /// </summary>
        [HttpPost("buy-additional-modules")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> BuyAdditionalModules([FromBody] int[] newModuleIds)
        {
            var tenantId = _currentTenant.TenantId;
            if (!tenantId.HasValue)
            {
                return Unauthorized(new { Error = "Tenant not found." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { Error = "User is not authenticated correctly." });
            }

            // 2. Tìm ngày hết hạn chung của Tenant để truyền vào tính prorate
            var tenant = await _systemTenantService.GetByIdAsync(tenantId.Value);
            if(tenant == null) 
                return NotFound(new { Error = "Tenant does not exist." });

            var expireDate = tenant.SubscriptionEndDate;
            DateTime? prorateDate = expireDate.HasValue 
                ? expireDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) 
                : null;

            // 3. Gọi hàm Backend tạo đơn hàng mua thêm:
            var order = await _billingOrderService.CreateModuleBillingOrderAsync(
                tenantId: tenantId.Value,
                customerId: userId,
                moduleIds: newModuleIds,
                isTrialOrder: false,
                prorateUntilUtc: prorateDate // Tính giá tiền cho những ngày còn lại
            );

            // 4. Lấy IP Client
            string? clientIp = null;
            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                clientIp = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            }
            clientIp ??= HttpContext.Connection.RemoteIpAddress?.ToString();

            // 5. Tạo Link thanh toán VNPay và trả về cho FE redirect
            var paymentUrl = await _billingService.CreatePaymentUrlAsync(order.Id, clientIp);

            return Ok(new { OrderId = order.Id, PaymentUrl = paymentUrl });
        }

    }
}
