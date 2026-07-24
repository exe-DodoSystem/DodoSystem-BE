using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface ISystemBillingReadRepository
{
    Task<PagedResultDto<SystemBillingOrderListItemDto>> GetBillingOrdersAsync(
        SystemBillingOrderQueryDto query,
        CancellationToken cancellationToken);

    Task<SystemBillingOrderDetailDto?> GetBillingOrderAsync(
        Guid billingOrderId,
        CancellationToken cancellationToken);

    Task<PagedResultDto<SystemPaymentTransactionDto>> GetPaymentTransactionsAsync(
        SystemPaymentTransactionQueryDto query,
        CancellationToken cancellationToken);
}
