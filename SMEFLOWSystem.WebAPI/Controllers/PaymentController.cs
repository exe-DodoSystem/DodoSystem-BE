using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using SMEFLOWSystem.Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using SMEFLOWSystem.SharedKernel.Interfaces;

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
    }
}
