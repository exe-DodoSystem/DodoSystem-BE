using Microsoft.AspNetCore.Http;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.PaymentDtos;
using System;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IBillingService
    {
        Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null);
        Task<string?> ProcessVNPayCallbackAsync(IQueryCollection query);
        Task EnqueuePaymentLinkEmailAsync(Guid orderId, string adminEmail, string companyName, string? clientIp = null, string emailType = StatusEnum.EmailTypeNew);
        
        Task<string> BuildSimulatedVNPaySuccessQueryStringAsync(Guid orderId, string? gatewayTransactionId = null);
        Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload);
    }
}
