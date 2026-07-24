using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices.System;

public interface ISystemBillingService
{
    Task<PagedResultDto<SystemBillingOrderListItemDto>> GetBillingOrdersAsync(
        SystemBillingOrderQueryDto query,
        CancellationToken cancellationToken = default);

    Task<SystemBillingOrderDetailDto?> GetBillingOrderAsync(
        Guid billingOrderId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<SystemPaymentTransactionDto>> GetPaymentTransactionsAsync(
        SystemPaymentTransactionQueryDto query,
        CancellationToken cancellationToken = default);
}
