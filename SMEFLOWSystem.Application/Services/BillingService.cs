using Hangfire;
using Microsoft.AspNetCore.Http;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
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

        public BillingService(
            IPaymentService paymentService,
            IEmailService emailService,
            IBillingOrderRepository billingOrderRepo,
            IBillingOrderModuleRepository billingOrderModuleRepo,
            IModuleRepository moduleRepo,
            IBackgroundJobClient backgroundJobClient)
        {
            _paymentService = paymentService;
            _emailService = emailService;
            _billingOrderRepo = billingOrderRepo;
            _billingOrderModuleRepo = billingOrderModuleRepo;
            _moduleRepo = moduleRepo;
            _backgroundJobClient = backgroundJobClient;
        }

        public Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null)
            => _paymentService.CreatePaymentUrlAsync(orderId, clientIp);

        public Task<string?> ProcessVNPayCallbackAsync(IQueryCollection query)
            => _paymentService.ProcessVNPayCallbackAsync(query);

        public Task<string> BuildSimulatedVNPaySuccessQueryStringAsync(Guid orderId, string? gatewayTransactionId = null)
            => _paymentService.BuildSimulatedVNPaySuccessQueryStringAsync(orderId, gatewayTransactionId);

        public async Task EnqueuePaymentLinkEmailAsync(Guid orderId, string adminEmail, string companyName, string? clientIp = null)
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

            string emailBody = $@"
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
                    <p>Nếu bạn muốn thanh toán ngay, vui lòng bấm vào link dưới đây:</p>
                    <a href='{paymentUrl}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none;'>THANH TOÁN (TUỲ CHỌN)</a>
                    <p>Hoặc copy link: {paymentUrl}</p>";

            _backgroundJobClient.Enqueue(() => _emailService.SendEmailAsync(adminEmail, "SMEFLOW - Link thanh toán (tuỳ chọn)", emailBody));
        }
    }
}
