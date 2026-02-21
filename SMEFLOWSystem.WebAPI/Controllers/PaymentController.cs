using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.Payment;
using SMEFLOWSystem.Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : Controller
    {
        private readonly IBillingService _billingService;
        private readonly IConfiguration _config;

        public PaymentController(IBillingService billingService, IConfiguration config)
        {
            _billingService = billingService;
            _config = config;
        }

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

        [HttpGet("callback/vnpay")]  
        public async Task<IActionResult> VNPayCallback([FromQuery] string? vnp_ResponseCode, [FromQuery] string? vnp_TxnRef)
        {
            try
            {
                var processed = await _billingService.ProcessVNPayCallbackAsync(Request.Query);

                // TODO: Thay đổi URL này thành frontend URL thực của bạn
                var frontendUrl = _config["Payment:FrontendUrl"] ?? "http://localhost:3000";

                if (!processed)
                {
                    return Redirect($"{frontendUrl}/payment/error");
                }

                var isSuccess = vnp_ResponseCode == "00";
                var orderId = vnp_TxnRef;
                
                return Redirect(isSuccess
                    ? $"{frontendUrl}/payment/success?orderId={orderId}"
                    : $"{frontendUrl}/payment/failed?orderId={orderId}");
            }
            catch (Exception ex)
            {
                // Log error nếu cần: _logger.LogError(ex, "VNPay callback failed");
                var frontendUrl = _config["Payment:FrontendUrl"] ?? "http://localhost:3000";
                return Redirect($"{frontendUrl}/payment/error");
            }
        }
    }
}
