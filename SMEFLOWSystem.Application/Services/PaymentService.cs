using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IBillingOrderRepository _billingOrderRepo;
        private readonly ITenantRepository _tenantRepo;
        private readonly IPaymentTransactionRepository _paymentTransactionRepo;
        private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;
        private readonly IModuleSubscriptionRepository _moduleSubscriptionRepo;
        private readonly IEmailService _emailService;
        private readonly ITransaction _transaction;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IUserRepository _userRepo;
        private readonly IConfiguration _config;

        public PaymentService(
            IBillingOrderRepository billingOrderRepo,
            ITenantRepository tenantRepo,
            IPaymentTransactionRepository paymentTransactionRepo,
            IBillingOrderModuleRepository billingOrderModuleRepo,
            IModuleSubscriptionRepository moduleSubscriptionRepo,
            IEmailService emailService,
            ITransaction transaction,
            IBackgroundJobClient backgroundJobClient,
            IConfiguration configuration,
            IUserRepository userRepo)
        {
            _billingOrderRepo = billingOrderRepo;
            _tenantRepo = tenantRepo;
            _paymentTransactionRepo = paymentTransactionRepo;
            _billingOrderModuleRepo = billingOrderModuleRepo;
            _moduleSubscriptionRepo = moduleSubscriptionRepo;
            _emailService = emailService;
            _transaction = transaction;
            _config = configuration;
            _backgroundJobClient = backgroundJobClient;
            _userRepo = userRepo;
        }

        public async Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null)
        {
            // Payment creation may happen without tenant context (e.g., after RegisterTenant) -> bypass tenant filters.
            var billingOrder = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (billingOrder == null) throw new Exception("Không tìm thấy đơn thanh toán");

            var mode = _config["Payment:Mode"] ?? throw new Exception("Missing config: Payment:Mode");
            if (mode == "Sandbox" || mode == "Production")
            {
                var gateway = _config["Payment:Gateway"] ?? throw new Exception("Missing config: Payment:Gateway");
                if (gateway == "VNPay")
                {
                    return await CreateVNPayUrlAsync(billingOrder, clientIp);
                }
                // Thêm Momo nếu cần sau
            }
            throw new Exception("Invalid payment mode");
        }

        public async Task<bool> ProcessVNPayCallbackAsync(IQueryCollection query)
        {
            // Verify signature từ VNPay callback (query params)
            var vnpayData = query.ToDictionary(q => q.Key, q => q.Value.ToString());
            if (!vnpayData.ContainsKey("vnp_SecureHash")) return false;

            var secureHash = vnpayData["vnp_SecureHash"];
            vnpayData.Remove("vnp_SecureHash");
            vnpayData.Remove("vnp_SecureHashType");

            var sortedData = vnpayData.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value);
            var queryString = string.Join("&", sortedData.Select(kv => $"{kv.Key}={kv.Value}"));
            var hashSecret = _config["Payment:VNPay:HashSecret"] ?? throw new Exception("Missing config: Payment:VNPay:HashSecret");
            var checkHash = HmacSha512(queryString, hashSecret);

            if (!string.Equals(checkHash, secureHash, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Invalid signature");

            if (!vnpayData.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
                throw new Exception("Missing vnp_TxnRef");

            if (!vnpayData.TryGetValue("vnp_ResponseCode", out var responseCode) || string.IsNullOrWhiteSpace(responseCode))
                throw new Exception("Missing vnp_ResponseCode");

            if (!vnpayData.TryGetValue("vnp_TransactionNo", out var gatewayTransactionId) || string.IsNullOrWhiteSpace(gatewayTransactionId))
                throw new Exception("Missing vnp_TransactionNo");

            var orderId = Guid.Parse(txnRef);
            var status = responseCode == "00" ? "Success" : "Failed";

            // Idempotency: if this gateway transaction was already processed, return OK.
            var existingTx = await _paymentTransactionRepo.GetByGatewayTransactionIdAsync(
                gateway: "VNPay",
                gatewayTransactionId: gatewayTransactionId,
                ignoreTenantFilter: true);
            if (existingTx != null) return true;

            // Callback has no tenant context -> bypass tenant filters when loading order.
            var orderForTenant = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (orderForTenant == null) throw new Exception("Không tìm thấy đơn thanh toán");

            decimal amount = 0m;
            if (vnpayData.TryGetValue("vnp_Amount", out var amountRaw) && long.TryParse(amountRaw, out var amountMinor))
            {
                amount = amountMinor / 100m;
            }

            // Process transactionally
            await _transaction.ExecuteAsync(async () =>
            {
                var existingInside = await _paymentTransactionRepo.GetByGatewayTransactionIdAsync(
                    gateway: "VNPay",
                    gatewayTransactionId: gatewayTransactionId,
                    ignoreTenantFilter: true);
                if (existingInside != null) return;

                var order = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
                if (order == null) throw new Exception("Không tìm thấy đơn thanh toán");

                var paymentTransaction = new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    BillingOrderId = order.Id,
                    Gateway = "VNPay",
                    GatewayTransactionId = gatewayTransactionId,
                    GatewayResponseCode = responseCode,
                    Amount = amount,
                    Status = status,
                    RawData = JsonConvert.SerializeObject(vnpayData),
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow
                };

                await _paymentTransactionRepo.AddAsync(paymentTransaction);

                if (status == "Success")
                {
                    if (BillingStateMachine.CanSetPaymentToPaid(order.PaymentStatus)
                        && BillingStateMachine.CanSetOrderToCompleted(order.Status))
                    {
                        order.PaymentStatus = StatusEnum.PaymentPaid;
                        order.Status = StatusEnum.OrderCompleted;
                    }
                }
                else
                {
                    if (BillingStateMachine.CanSetPaymentToFailed(order.PaymentStatus)
                        && BillingStateMachine.CanSetOrderToCancelled(order.Status))
                    {
                        order.PaymentStatus = StatusEnum.PaymentFailed;
                        order.Status = StatusEnum.OrderCancelled;
                    }
                }
                await _billingOrderRepo.UpdateIgnoreTenantAsync(order);
            });

            // Nếu thanh toán thành công, dùng Hangfire để active Tenant và Owner (background job để không block callback)
            if (status == "Success")
            {
                _backgroundJobClient.Enqueue(() => ActivateTenantAfterPaymentAsync(orderId, gatewayTransactionId));
            }

            return true;
        }

        // Background job để active Tenant và gửi email (gọi từ Hangfire)
        public async Task ActivateTenantAfterPaymentAsync(Guid orderId, string transactionId)
        {
            string? ownerEmail = null;
            string? tenantName = null;
            bool shouldSendEmail = false;

            await _transaction.ExecuteAsync(async () =>
            {
                var order = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
                if (order == null) throw new Exception("Không tìm thấy đơn thanh toán");

                // Double-activation guard: only proceed for paid orders
                if (!string.Equals(order.PaymentStatus, StatusEnum.PaymentPaid, StringComparison.OrdinalIgnoreCase))
                    return;

                var tenant = await _tenantRepo.GetByIdIgnoreTenantAsync(order.TenantId);
                if (tenant == null) throw new Exception("Không tìm thấy tenant");

                tenantName = tenant.Name;

                if (!BillingStateMachine.CanActivateTenant(tenant.Status)
                    && !string.Equals(tenant.Status, StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var orderModules = await _billingOrderModuleRepo.GetByBillingOrderIdIgnoreTenantAsync(order.Id);
                if (orderModules.Count == 0)
                    throw new Exception("Đơn thanh toán không có module nào");

                var now = DateTime.UtcNow;
                DateTime maxEndDate = now;
                foreach (var line in orderModules)
                {
                    var existingSub = await _moduleSubscriptionRepo.GetByTenantAndModuleIgnoreTenantAsync(tenant.Id, line.ModuleId);
                    if (existingSub == null)
                    {
                        existingSub = new ModuleSubscription
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ModuleId = line.ModuleId,
                            StartDate = now,
                            EndDate = now,
                            Status = StatusEnum.ModuleActive,
                            CreatedAt = now,
                            IsDeleted = false
                        };
                        await _moduleSubscriptionRepo.AddAsync(existingSub);
                    }

                    var baseDate = existingSub.EndDate > now ? existingSub.EndDate : now;
                    existingSub.EndDate = baseDate.AddMonths(1);
                    existingSub.Status = StatusEnum.ModuleActive;
                    await _moduleSubscriptionRepo.UpdateIgnoreTenantAsync(existingSub);

                    if (existingSub.EndDate > maxEndDate) 
                        maxEndDate = existingSub.EndDate;
                }

                tenant.Status = StatusEnum.TenantActive;
                tenant.SubscriptionEndDate = DateOnly.FromDateTime(maxEndDate);

                var ownerUser = tenant.OwnerUserId.HasValue ? await _userRepo.GetByIdIgnoreTenantAsync(tenant.OwnerUserId.Value) : null;
                if (ownerUser != null)
                {
                    ownerUser.IsActive = true;
                    await _userRepo.UpdateUserIgnoreTenantAsync(ownerUser);
                    ownerEmail = ownerUser.Email;
                }

                await _tenantRepo.UpdateIgnoreTenantAsync(tenant);
                shouldSendEmail = !string.IsNullOrWhiteSpace(ownerEmail);
            });

            if (shouldSendEmail && ownerEmail != null && tenantName != null)
            {
                await _emailService.SendEmailAsync(
                    ownerEmail,
                    "Thanh toán thành công - Kích hoạt tài khoản SMEFLOW",
                    $"<h3>Chúc mừng {tenantName}!</h3><p>Tài khoản của bạn đã được kích hoạt.</p><p>Mã giao dịch: {transactionId}</p><p>Bạn có thể đăng nhập ngay bây giờ.</p>"
                );
            }
        }

        private string HmacSha512(string data, string key)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private Task<string> CreateVNPayUrlAsync(BillingOrder order, string? clientIp)
        {
            // Logic tạo URL VNPay (dựa trên docs sandbox.vnpayment.vn)
            clientIp = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp;

            var tmnCode = _config["Payment:VNPay:TmnCode"] ?? throw new Exception("Missing config: Payment:VNPay:TmnCode");
            var returnUrl = _config["Payment:VNPay:ReturnUrl"] ?? throw new Exception("Missing config: Payment:VNPay:ReturnUrl");
            var hashSecret = _config["Payment:VNPay:HashSecret"] ?? throw new Exception("Missing config: Payment:VNPay:HashSecret");
            var paymentBaseUrl = _config["Payment:VNPay:PaymentUrl"] ?? throw new Exception("Missing config: Payment:VNPay:PaymentUrl");

            var discount = order.DiscountAmount ?? 0m;
            var payable = order.TotalAmount - discount;
            if (payable <= 0m)
                throw new Exception("Đơn thanh toán không hợp lệ (số tiền phải > 0)");

            var amountMinor = checked((long)decimal.Round(payable * 100m, 0, MidpointRounding.AwayFromZero));

            var vnpayParams = new Dictionary<string, string>
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode,
                ["vnp_Amount"] = amountMinor.ToString(CultureInfo.InvariantCulture), // VND * 100
                ["vnp_CreateDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                ["vnp_CurrCode"] = "VND",
                ["vnp_IpAddr"] = clientIp,
                ["vnp_Locale"] = "vn",
                ["vnp_OrderInfo"] = $"Thanh toan don hang {order.BillingOrderNumber}",
                ["vnp_OrderType"] = "billpayment",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_TxnRef"] = order.Id.ToString() // OrderId làm ref
            };
            // Sort and hash params
            var sortedParams = vnpayParams.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value);
            var queryString = string.Join("&", sortedParams.Select(kv => $"{kv.Key}={kv.Value}"));
            var hash = HmacSha512(queryString, hashSecret);
            vnpayParams["vnp_SecureHash"] = hash;
            // Build URL
            var paymentUrl = paymentBaseUrl + "?" + string.Join("&", vnpayParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            return Task.FromResult(paymentUrl);
        }
    }
}