using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using SMEFLOWSystem.Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Authorization;
using SMEFLOWSystem.Application.DTOs.PaymentDtos;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using SMEFLOWSystem.Application.Interfaces.IRepositories;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : Controller
    {
        private readonly IBillingService _billingService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public PaymentController(
            IBillingService billingService,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _billingService = billingService;
            _config = config;
            _env = env;
        }

        /// <summary>Tạo URL thanh toán VNPay cho đơn hàng</summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment([FromQuery] Guid orderId)
        {
            string? clientIp = null;
            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                clientIp = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            }

            clientIp ??= HttpContext.Connection.RemoteIpAddress?.ToString();

            var url = await _billingService.CreatePaymentUrlAsync(orderId, clientIp);
            return Ok(url);  
        }

        /// <summary>Callback IPN nhận kết quả thanh toán từ VNPay</summary>
        [HttpGet("callback/vnpay")]
        public async Task<IActionResult> VNPayCallback([FromQuery] string? vnp_TxnRef)
        {
            var frontendUrl = _config["Payment:FrontendUrl"] ?? "http://localhost:3000";
            try
            {
                var status = await _billingService.ProcessVNPayCallbackAsync(Request.Query);

                if (status == null)
                {
                    return Redirect($"{frontendUrl}/payment/error");
                }

                return Redirect(status == "Success"
                    ? $"{frontendUrl}/payment/success?orderId={vnp_TxnRef}"
                    : $"{frontendUrl}/payment/failed?orderId={vnp_TxnRef}");
            }
            catch (Exception ex)
            {
                return Redirect($"{frontendUrl}/payment/error");
            }
        }

        /// <summary>[Dev only] Giả lập thanh toán VNPay thành công</summary>
        [HttpPost("simulate/vnpay/success")]
        public async Task<IActionResult> SimulateVNPaySuccess([FromQuery] Guid orderId, [FromQuery] string? vnp_TransactionNo = null)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var queryString = await _billingService.BuildSimulatedVNPaySuccessQueryStringAsync(orderId, vnp_TransactionNo);
            var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/payment/callback/vnpay?{queryString}";
            var parsed = QueryHelpers.ParseQuery(queryString);
            var status = await _billingService.ProcessVNPayCallbackAsync(new QueryCollection(parsed));

            return Ok(new
            {
                OrderId = orderId,
                Status = status,
                CallbackUrl = callbackUrl
            });
        }

        /// <summary>[Dev only] Giả lập webhook thanh toán SePay thành công</summary>
        [HttpPost("simulate/sepay/success")]
        public async Task<IActionResult> SimulateSePaySuccess(
            [FromServices] IBillingOrderRepository billingOrderRepo,
            [FromQuery] Guid orderId, 
            [FromQuery] string? transactionCode = null)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var order = await billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Order not found" });

            var discount = order.DiscountAmount ?? 0m;
            var payable = order.TotalAmount - discount;
            var code = transactionCode ?? $"SIM-{DateTime.UtcNow.Ticks.ToString().Substring(10)}";

            var payload = new SePayWebhookPayload(
                Id: DateTime.UtcNow.Ticks % 10000000,
                Gateway: "MBBank",
                TransactionDate: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AccountNumber: "123456789",
                SubAccount: null,
                TransferAmount: payable,
                Accumulated: 99999999,
                Code: code,
                Content: $"DODO {order.BillingOrderNumber}",
                ReferenceCode: code,
                Description: "Simulated payment",
                TransferType: "in"
            );

            var success = await _billingService.ProcessSePayWebhookAsync(payload);
            return Ok(new
            {
                Success = success,
                Payload = payload
            });
        }

        /// <summary>Webhook nhận kết quả thanh toán từ SePay</summary>
        [HttpPost("webhook/sepay")]
        [AllowAnonymous]
        public async Task<IActionResult> SePayWebhook()
        {
            var signatureHeader = Request.Headers["x-sepay-signature"].ToString();
            
            Request.EnableBuffering();
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            // Verify API Key
            var expectedApiKey = _config["Payment:SePay:ApiKey"];
            if (!string.IsNullOrEmpty(expectedApiKey))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                var providedKey = authHeader
                    .Replace("Sepay ", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (providedKey != expectedApiKey)
                    return Unauthorized(new { success = false, message = "Invalid API Key" });
            }

            // Verify Signature
            var expectedSecret = _config["Payment:SePay:WebhookSecret"];
            if (!string.IsNullOrEmpty(expectedSecret))
            {
                if (string.IsNullOrEmpty(signatureHeader))
                {
                    return Unauthorized(new { success = false, message = "Missing signature header" });
                }

                var keyBytes = Encoding.UTF8.GetBytes(expectedSecret);
                var payloadBytes = Encoding.UTF8.GetBytes(rawBody);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(payloadBytes);
                var computedSignature = Convert.ToHexString(hashBytes).ToLower();

                if (!string.Equals(signatureHeader.Trim(), computedSignature, StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new { success = false, message = "Invalid Signature" });
                }
            }

            var payload = JsonConvert.DeserializeObject<SePayWebhookPayload>(rawBody);
            if (payload == null)
            {
                return BadRequest(new { success = false, message = "Invalid JSON payload" });
            }

            var result = await _billingService.ProcessSePayWebhookAsync(payload);
            return Ok(new { success = result });
        }
    }
}
