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
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.WebAPI.Security;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : Controller
    {
        private readonly IBillingService _billingService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IBillingService billingService,
            IConfiguration config,
            IWebHostEnvironment env,
            ILogger<PaymentController> logger)
        {
            _billingService = billingService;
            _config = config;
            _env = env;
            _logger = logger;
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
            catch (Exception)
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

            var payable = order.TotalAmount;
            var code = transactionCode ?? $"SIM-{DateTime.UtcNow.Ticks.ToString().Substring(10)}";

            var payload = new SePayWebhookPayload(
                Id: (DateTime.UtcNow.Ticks % 10000000).ToString(),
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

            var result = await _billingService.ProcessSePayWebhookAsync(payload);
            return Ok(new
            {
                Success = result.Succeeded,
                result.ErrorCode,
                result.Message,
                Payload = payload
            });
        }

        /// <summary>Webhook nhận kết quả thanh toán từ SePay</summary>
        [HttpPost("webhook/sepay")]
        [AllowAnonymous]
        public async Task<IActionResult> SePayWebhook()
        {
            Request.EnableBuffering();
            byte[] rawBodyBytes;
            using (var buffer = new MemoryStream())
            {
                await Request.Body.CopyToAsync(buffer, HttpContext.RequestAborted);
                rawBodyBytes = buffer.ToArray();
                Request.Body.Position = 0;
            }

            var authentication = SePayWebhookAuthentication.Authenticate(
                Request.Headers,
                rawBodyBytes,
                _config["Payment:SePay:ApiKey"],
                _config["Payment:SePay:WebhookSecret"],
                DateTimeOffset.UtcNow);

            if (!authentication.Succeeded)
            {
                _logger.LogWarning(
                    "Rejected SePay webhook authentication. Reason={Reason}, TraceId={TraceId}",
                    authentication.ErrorCode,
                    HttpContext.TraceIdentifier);
                return Unauthorized(new
                {
                    success = false,
                    code = authentication.ErrorCode,
                    message = "Webhook authentication failed.",
                    traceId = HttpContext.TraceIdentifier
                });
            }

            var rawBody = Encoding.UTF8.GetString(rawBodyBytes);
            SePayWebhookParseResult parseResult;
            try
            {
                parseResult = SePayWebhookPayloadParser.Parse(rawBody);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Rejected malformed SePay webhook JSON. TraceId={TraceId}",
                    HttpContext.TraceIdentifier);
                return BadRequest(new
                {
                    success = false,
                    code = "SEPAY_INVALID_JSON",
                    message = "Webhook body is not valid JSON.",
                    traceId = HttpContext.TraceIdentifier
                });
            }

            if (!parseResult.Succeeded)
            {
                _logger.LogWarning(
                    "Rejected unsupported SePay webhook payload. Fields={Fields}, TraceId={TraceId}",
                    parseResult.DetectedFields,
                    HttpContext.TraceIdentifier);
                return BadRequest(new
                {
                    success = false,
                    code = "SEPAY_UNSUPPORTED_PAYLOAD",
                    message = "Webhook payload schema is not supported.",
                    fields = parseResult.DetectedFields,
                    traceId = HttpContext.TraceIdentifier
                });
            }

            var payload = parseResult.Payload!;

            // SePay Dashboard uses id=0 for "Gửi thử". Authentication and JSON schema
            // have already been validated, so acknowledge it without touching business data.
            if (payload.Id == "0")
            {
                _logger.LogInformation(
                    "Accepted SePay test webhook. TraceId={TraceId}",
                    HttpContext.TraceIdentifier);
                return Ok(new { success = true });
            }

            var result = await _billingService.ProcessSePayWebhookAsync(payload);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "SePay webhook was authenticated but not matched. SePayId={SePayId}, ErrorCode={ErrorCode}, TraceId={TraceId}",
                    payload.Id,
                    result.ErrorCode,
                    HttpContext.TraceIdentifier);

                return Ok(new
                {
                    success = false,
                    code = result.ErrorCode,
                    message = result.Message,
                    traceId = HttpContext.TraceIdentifier
                });
            }

            return Ok(new { success = true });
        }
    }
}
