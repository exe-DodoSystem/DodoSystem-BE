using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Events.Notification;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using SMEFLOWSystem.Application.DTOs.PaymentDtos;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class BillingService : IBillingService
    {
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly IBillingOrderRepository _billingOrderRepo;
        private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;
        private readonly IModuleRepository _moduleRepo;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IOutboxMessageRepository _outboxMessageRepo;
        private readonly IConfiguration _config;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        public BillingService(
            IPaymentService paymentService,
            IEmailService emailService,
            IBillingOrderRepository billingOrderRepo,
            IBillingOrderModuleRepository billingOrderModuleRepo,
            IModuleRepository moduleRepo,
            IBackgroundJobClient backgroundJobClient,
            IOutboxMessageRepository outboxMessageRepo,
            IConfiguration config,
            ITenantRepository tenantRepository,
            IUserRepository userRepository)

        {
            _paymentService = paymentService;
            _emailService = emailService;
            _billingOrderRepo = billingOrderRepo;
            _billingOrderModuleRepo = billingOrderModuleRepo;
            _moduleRepo = moduleRepo;
            _backgroundJobClient = backgroundJobClient;
            _outboxMessageRepo = outboxMessageRepo;
            _config = config;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
        }

        public Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null)
            => _paymentService.CreatePaymentUrlAsync(orderId, clientIp);

        public Task<string?> ProcessVNPayCallbackAsync(IQueryCollection query)
            => _paymentService.ProcessVNPayCallbackAsync(query);

        public Task<string> BuildSimulatedVNPaySuccessQueryStringAsync(Guid orderId, string? gatewayTransactionId = null)
            => _paymentService.BuildSimulatedVNPaySuccessQueryStringAsync(orderId, gatewayTransactionId);

        public Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload)
            => _paymentService.ProcessSePayWebhookAsync(payload);

        public async Task EnqueuePaymentLinkEmailAsync(Guid orderId, string adminEmail, string companyName, string? clientIp = null, string emailType = StatusEnum.EmailTypeNew)
        {
            var paymentUrl = await _paymentService.CreatePaymentUrlAsync(orderId, clientIp);

            var order = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (order == null) throw new Exception("Không tìm thấy đơn thanh toán");

            var orderLines = await _billingOrderModuleRepo.GetByBillingOrderIdIgnoreTenantAsync(orderId);
            var moduleIds = orderLines.Select(x => x.ModuleId).Distinct().ToArray();
            var modules = moduleIds.Length == 0 ? new() : await _moduleRepo.GetByIdsAsync(moduleIds);

            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var discount = order.DiscountAmount ?? 0m;
            var payable = order.TotalAmount - discount;

            var linesHtml = new StringBuilder();
            if (orderLines.Count > 0)
            {
                linesHtml.Append("<ul>");
                foreach (var line in orderLines)
                {
                    var moduleName = modules.FirstOrDefault(m => m.Id == line.ModuleId)?.Name ?? $"Module #{line.ModuleId}";
                    linesHtml.Append($"<li>{moduleName}: {line.LineTotal.ToString("N0", vi)} VND</li>");
                }
                linesHtml.Append("</ul>");
            }

            var gateway = _config["Payment:Gateway"];
            var isSePay = string.Equals(gateway, "SePay", StringComparison.OrdinalIgnoreCase);
            SePayPaymentInfoDto? sePayInfo = null;
            if (isSePay)
            {
                try
                {
                    sePayInfo = JsonConvert.DeserializeObject<SePayPaymentInfoDto>(paymentUrl);
                }
                catch { }
            }

            string paymentActionHtml;
            if (sePayInfo != null)
            {
                paymentActionHtml = $@"
                    <p>Vui lòng chuyển khoản thanh toán bằng cách quét mã QR dưới đây hoặc chuyển khoản thủ công theo thông tin:</p>
                    <table style='border-collapse: collapse; width: 100%; max-width: 500px; margin-bottom: 15px;'>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background-color: #f9f9f9; width: 40%;'><b>Ngân hàng nhận:</b></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{sePayInfo.BankCode}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background-color: #f9f9f9;'><b>Số tài khoản:</b></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'><b>{sePayInfo.BankAccountNumber}</b></td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background-color: #f9f9f9;'><b>Tên tài khoản:</b></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{sePayInfo.BankAccountName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background-color: #f9f9f9;'><b>Số tiền chuyển:</b></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'><b>{payable.ToString("N0", vi)} VND</b></td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background-color: #f9f9f9;'><b>Nội dung chuyển khoản:</b></td>
                            <td style='padding: 8px; border: 1px solid #ddd; color: #d9534f;'><b>{sePayInfo.TransferContent}</b></td>
                        </tr>
                    </table>
                    <p style='color: #d9534f; font-weight: bold;'>⚠️ Quan trọng: Vui lòng ghi chính xác nội dung chuyển khoản ở trên để hệ thống tự động kích hoạt dịch vụ ngay lập tức.</p>
                    <div style='margin-top: 15px;'>
                        <img src='{sePayInfo.QrCodeUrl}' alt='Mã QR Thanh toán VietQR' style='max-width: 250px; border: 1px solid #ccc; padding: 5px; background: #fff;' />
                    </div>";
            }
            else
            {
                paymentActionHtml = $@"
                    <p>Vui lòng bấm vào link dưới đây để tiến hành thanh toán:</p>
                    <a href='{paymentUrl}' style='padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; display: inline-block; border-radius: 4px;'>THANH TOÁN ĐƠN HÀNG</a>
                    <p>Hoặc copy link: {paymentUrl}</p>";
            }

            string emailBody;
            string emailSubject;

            if (emailType == StatusEnum.EmailTypeTrialOptional)
            {
                emailSubject = "DodoSystem - Link thanh toán (tuỳ chọn)";
                emailBody = $@"
                    <h3>Chào mừng {companyName} đến với SMEFLOW!</h3>
                    <p>Bạn đã đăng ký thành công và đang được dùng <b>miễn phí 14 ngày</b> (Free Trial).</p>
                    <p><b>Bạn có thể đăng nhập và sử dụng ngay</b> trong thời gian dùng thử — không cần thanh toán để bắt đầu.</p>
                    <p>Nếu bạn muốn thanh toán sớm, sau khi hệ thống nhận thanh toán thành công, chúng tôi sẽ:</p>
                    <ul>
                        <li>Chuyển trạng thái dịch vụ sang <b>Active</b></li>
                        <li><b>Cộng thêm 01 tháng</b> vào ngày hết hạn hiện tại (tính từ thời điểm hết hạn đang có — bao gồm cả trial nếu còn)</li>
                    </ul>
                    <hr/>
                    <p><b>Thông tin đơn hàng (tuỳ chọn thanh toán)</b></p>
                    <p>Mã đơn: <b>{order.BillingOrderNumber}</b></p>
                    {linesHtml}
                    <p>Tổng tiền: <b>{order.TotalAmount.ToString("N0", vi)} VND</b></p>
                    <p>Giảm giá: <b>{discount.ToString("N0", vi)} VND</b></p>
                    <p>Cần thanh toán: <b>{payable.ToString("N0", vi)} VND</b></p>
                    <hr/>
                    {paymentActionHtml}";
            }
            else if (emailType == StatusEnum.EmailTypeAdditional)
            {
                emailSubject = "DodoSystem - Link thanh toán mua thêm Module";
                emailBody = $@"
                    <h3>Xin chào {companyName},</h3>
                    <p>Bạn vừa tạo thành công đơn hàng <b>mua thêm Module</b> trên hệ thống SMEFLOW.</p>
                    <p>Chi phí cho đơn hàng này được tính toán tự động dựa trên số ngày còn lại của gói dịch vụ hiện tại.</p>
                    <hr/>
                    <p><b>Thông tin đơn hàng mua thêm</b></p>
                    <p>Mã đơn: <b>{order.BillingOrderNumber}</b></p>
                    {linesHtml}
                    <p>Tổng tiền: <b>{order.TotalAmount.ToString("N0", vi)} VND</b></p>
                    <p>Giảm giá: <b>{discount.ToString("N0", vi)} VND</b></p>
                    <p>Cần thanh toán: <b>{payable.ToString("N0", vi)} VND</b></p>
                    <hr/>
                    {paymentActionHtml}";
            }
            else if (emailType == StatusEnum.EmailTypeRenewal)
            {
                emailSubject = "DodoSystem - Link thanh toán gia hạn dịch vụ";
                emailBody = $@"
                    <h3>Xin chào {companyName},</h3>
                    <p>Hệ thống vừa tạo đơn hàng <b>gia hạn dịch vụ</b> định kỳ cho tài khoản của bạn.</p>
                    <hr/>
                    <p><b>Thông tin đơn hàng gia hạn</b></p>
                    <p>Mã đơn: <b>{order.BillingOrderNumber}</b></p>
                    {linesHtml}
                    <p>Tổng tiền: <b>{order.TotalAmount.ToString("N0", vi)} VND</b></p>
                    <p>Giảm giá: <b>{discount.ToString("N0", vi)} VND</b></p>
                    <p>Cần thanh toán: <b>{payable.ToString("N0", vi)} VND</b></p>
                    <hr/>
                    {paymentActionHtml}";
            }
            else // emailType == StatusEnum.EmailTypeNew or default
            {
                emailSubject = "DodoSystem - Link thanh toán đơn hàng";
                emailBody = $@"
                    <h3>Xin chào {companyName},</h3>
                    <p>Bạn đã tạo thành công đơn hàng khởi tạo dịch vụ trên SMEFLOW.</p>
                    <hr/>
                    <p><b>Thông tin đơn hàng</b></p>
                    <p>Mã đơn: <b>{order.BillingOrderNumber}</b></p>
                    {linesHtml}
                    <p>Tổng tiền: <b>{order.TotalAmount.ToString("N0", vi)} VND</b></p>
                    <p>Giảm giá: <b>{discount.ToString("N0", vi)} VND</b></p>
                    <p>Cần thanh toán: <b>{payable.ToString("N0", vi)} VND</b></p>
                    <hr/>
                    {paymentActionHtml}";
            }

            var currentTenant = await _tenantRepository.GetByIdIgnoreTenantAsync(order.TenantId);
            if (currentTenant == null)
            {
                return;
            }

            var targetEmail = adminEmail; 

            if (string.IsNullOrWhiteSpace(targetEmail) && currentTenant.OwnerUserId.HasValue)
            {
                var ownerUser = await _userRepository.GetByIdIgnoreTenantAsync(currentTenant.OwnerUserId.Value);
                if (ownerUser != null)
                {
                    targetEmail = ownerUser.Email ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(targetEmail))
                return;

            //await _emailService.SendEmailAsync(targetEmail, emailSubject, emailBody, CancellationToken.None);

            var emailEvent = new EmailNotificationRequestedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                TenantId = currentTenant.Id,
                ToEmail = targetEmail,
                Subject = emailSubject,
                Body = emailBody,
                CorrelationId = orderId.ToString()
            };

            var exchange = _config["RabbitMQ:Exchange"] ?? "smeflow.exchange";
            var routingKey = _config["RabbitMQ:RoutingKeys:SendEmail"] ?? "email.send";

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = currentTenant.Id,
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

            await _outboxMessageRepo.AddAsync(outboxMessage);
        }
    }
}
