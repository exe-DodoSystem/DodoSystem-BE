using Microsoft.AspNetCore.Http;
using SMEFLOWSystem.Application.DTOs.PaymentDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IPaymentService
    {
        //Task<bool> ProcessPaymentCallBackAsync(PaymentCallbackDto dto);
        Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null);  // Trả về URL thanh toán (redirect user đến đó)
        Task<string?> ProcessVNPayCallbackAsync(IQueryCollection query);  // Callback từ VNPay (query params), trả về "Success"/"Failed" hoặc null nếu không hợp lệ     
        Task<string> BuildSimulatedVNPaySuccessQueryStringAsync(Guid orderId, string? gatewayTransactionId = null);
        Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload);
        //Task<bool> ProcessMomoCallbackAsync(PaymentCallbackDto dto);
    }
} 
