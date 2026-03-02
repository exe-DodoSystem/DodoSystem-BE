using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using VNPAY.NET;
using VNPAY.NET.Enums;
using VNPAY.NET.Models;
using VNPAY.NET.Utilities;
using System.Globalization;
using System.Security.Cryptography;

namespace SMEFLOWSystem.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private const string GatewayVNPay = "VNPay";

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
        private readonly IVnpay _vnpay;

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
            IUserRepository userRepo,
            IVnpay vnpay)
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
            _vnpay = vnpay;
        }

        public async Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null)
        {
            var billingOrder = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (billingOrder == null) throw new Exception("Không tìm thấy đơn thanh toán");

            if (!string.Equals(billingOrder.PaymentStatus, StatusEnum.PaymentPending, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(billingOrder.Status, StatusEnum.OrderPending, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Đơn thanh toán không ở trạng thái chờ thanh toán");
            }

            var gateway = _config["Payment:Gateway"] ?? throw new Exception("Missing config: Payment:Gateway");
            if (gateway == "VNPay")
            {
                return CreateVNPayUrl(billingOrder, clientIp);
            }
            throw new Exception($"Unsupported payment gateway: {gateway}");
        }

        public async Task<string?> ProcessVNPayCallbackAsync(IQueryCollection query)
        {
            // Initialize VNPay library for signature verification
            InitializeVnpay();

            // Use VNPAY.NET library to verify signature and parse result
            var paymentResult = _vnpay.GetPaymentResult(query);

            var isSuccess = paymentResult.IsSuccess;
            var status = isSuccess ? "Success" : "Failed";

            if (!Guid.TryParse(paymentResult.PaymentId, out var orderId))
                return null;

            var gatewayTransactionId = paymentResult.VnpayTransactionId.ToString();

            // Callback has no tenant context -> bypass tenant filters when loading order.
            var orderForTenant = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (orderForTenant == null) throw new Exception("Không tìm thấy đơn thanh toán");

            // Validate amount — VNPay returns amount in minor units (x100)
            var vnpAmountStr = query
                .Where(q => q.Key.Equals("vnp_Amount", StringComparison.OrdinalIgnoreCase))
                .Select(q => q.Value.ToString())
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(vnpAmountStr))
                throw new Exception("Missing vnp_Amount");

            if (!long.TryParse(vnpAmountStr, out var amountMinor) || amountMinor <= 0)
                throw new Exception("Invalid vnp_Amount");

            var amount = amountMinor / 100m;

            // Validate amount matches order payable amount
            var discount = orderForTenant.DiscountAmount ?? 0m;
            var expectedPayable = orderForTenant.TotalAmount - discount;
            if (expectedPayable <= 0m)
                throw new Exception("Đơn thanh toán không hợp lệ (số tiền phải > 0)");
            var expectedMinor = checked((long)decimal.Round(expectedPayable * 100m, 0, MidpointRounding.AwayFromZero));
            if (amountMinor != expectedMinor)
                throw new Exception("Số tiền thanh toán không khớp đơn hàng");

            var vnpResponseCode = query
                .Where(q => q.Key.Equals("vnp_ResponseCode", StringComparison.OrdinalIgnoreCase))
                .Select(q => q.Value.ToString())
                .FirstOrDefault() ?? "unknown";

            // Collect raw callback data for auditing
            var queryParams = query
                .Where(q => !string.IsNullOrWhiteSpace(q.Key) && q.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            // Process transactionally
            var shouldEnqueueActivation = false;
            await _transaction.ExecuteAsync(async () =>
            {
                var existingInside = await _paymentTransactionRepo.GetByGatewayTransactionIdAsync(
                    gateway: GatewayVNPay,
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
                    Gateway = GatewayVNPay,
                    GatewayTransactionId = gatewayTransactionId,
                    GatewayResponseCode = vnpResponseCode,
                    Amount = amount,
                    Status = status,
                    RawData = JsonConvert.SerializeObject(queryParams),
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow
                };

                await _paymentTransactionRepo.AddAsync(paymentTransaction);

                if (isSuccess)
                {
                    if (BillingStateMachine.CanSetPaymentToPaid(order.PaymentStatus)
                        && BillingStateMachine.CanSetOrderToCompleted(order.Status))
                    {
                        order.PaymentStatus = StatusEnum.PaymentPaid;
                        order.Status = StatusEnum.OrderCompleted;
                        shouldEnqueueActivation = true;
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
            if (shouldEnqueueActivation)
            {
                _backgroundJobClient.Enqueue(() => ActivateTenantAfterPaymentAsync(orderId, gatewayTransactionId));
            }

            return status;
        }

        public async Task<string> BuildSimulatedVNPaySuccessQueryStringAsync(Guid orderId, string? gatewayTransactionId = null)
        {
            var order = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (order == null) throw new Exception("Không tìm thấy đơn thanh toán");

            var discount = order.DiscountAmount ?? 0m;
            var payable = order.TotalAmount - discount;
            if (payable <= 0m)
                throw new Exception("Đơn thanh toán không hợp lệ (số tiền phải > 0)");

            var expectedMinor = checked((long)decimal.Round(payable * 100m, 0, MidpointRounding.AwayFromZero));

            var tmnCode = _config["Payment:VNPay:TmnCode"] ?? throw new Exception("Missing config: Payment:VNPay:TmnCode");
            var hashSecret = _config["Payment:VNPay:HashSecret"] ?? throw new Exception("Missing config: Payment:VNPay:HashSecret");

            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var payDate = vnTime.ToString("yyyyMMddHHmmss");

            var transactionNo = string.IsNullOrWhiteSpace(gatewayTransactionId)
                ? RandomNumberGenerator.GetInt32(10000000, 99999999).ToString()
                : gatewayTransactionId.Trim();

            // Use VNPAY.NET's PaymentHelper to build the sign data consistently
            var helper = new PaymentHelper();
            helper.AddRequestData("vnp_TmnCode", tmnCode);
            helper.AddRequestData("vnp_Amount", expectedMinor.ToString(CultureInfo.InvariantCulture));
            helper.AddRequestData("vnp_TxnRef", order.Id.ToString());
            helper.AddRequestData("vnp_ResponseCode", "00");
            helper.AddRequestData("vnp_TransactionStatus", "00");
            helper.AddRequestData("vnp_TransactionNo", transactionNo);
            helper.AddRequestData("vnp_BankCode", "NCB");
            helper.AddRequestData("vnp_PayDate", payDate);
            helper.AddRequestData("vnp_OrderInfo", $"SIMULATE SUCCESS {order.BillingOrderNumber}");

            // GetPaymentUrl returns "baseUrl?signData&vnp_SecureHash=xxx"
            // We only need the query string part (after "?")
            var fullUrl = helper.GetPaymentUrl("https://simulate", hashSecret);
            var queryString = fullUrl.Substring("https://simulate?".Length);
            return queryString;
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

                if (!string.Equals(order.PaymentStatus, StatusEnum.PaymentPaid, StringComparison.OrdinalIgnoreCase))
                    return;

                var tenant = await _tenantRepo.GetByIdIgnoreTenantAsync(order.TenantId);
                if (tenant == null) throw new Exception("Không tìm thấy tenant");

                tenantName = tenant.Name;

                var canProceed = string.Equals(tenant.Status, StatusEnum.TenantActive, StringComparison.OrdinalIgnoreCase)
                                 || BillingStateMachine.CanActivateTenant(tenant.Status);
                if (!canProceed) return;

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

        private string CreateVNPayUrl(BillingOrder order, string? clientIp)
        {
            InitializeVnpay();

            var discount = order.DiscountAmount ?? 0m;
            var payable = order.TotalAmount - discount;
            if (payable <= 0m)
                throw new Exception("Đơn thanh toán không hợp lệ (số tiền phải > 0)");

            var ipAddress = clientIp ?? "127.0.0.1";

            var orderInfo = $"Thanh toan don {order.BillingOrderNumber}";
            if (orderInfo.Length > 100) orderInfo = orderInfo[..100];

            var request = new PaymentRequest
            {
                PaymentId = order.Id.ToString(),
                Money = (double)payable,
                Description = orderInfo,
                IpAddress = ipAddress,
                BankCode = BankCode.ANY,
                CreatedDate = DateTime.Now,
                Currency = Currency.VND,
                Language = DisplayLanguage.Vietnamese
            };

            return _vnpay.GetPaymentUrl(request);
        }

        /// <summary>
        /// Initialize VNPAY.NET library with config values
        /// </summary>
        private void InitializeVnpay()
        {
            var vnpUrl = _config["Payment:VNPay:BaseUrl"] ?? throw new Exception("Missing config: Payment:VNPay:BaseUrl");
            var tmnCode = _config["Payment:VNPay:TmnCode"] ?? throw new Exception("Missing config: Payment:VNPay:TmnCode");
            var hashSecret = _config["Payment:VNPay:HashSecret"] ?? throw new Exception("Missing config: Payment:VNPay:HashSecret");
            var callbackUrl = _config["Payment:VNPay:CallbackUrl"] ?? throw new Exception("Missing config: Payment:VNPay:CallbackUrl");

            _vnpay.Initialize(tmnCode, hashSecret, vnpUrl, callbackUrl);
        }
    }
}