using SharedKernel.DTOs;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices.System;

namespace SMEFLOWSystem.Application.Services.System;

public sealed class SystemBillingService : ISystemBillingService
{
    private static readonly string[] BillingSortFields =
        ["billingDate", "createdAt", "finalAmount", "billingOrderNumber"];
    private static readonly string[] PaymentStatuses =
        [StatusEnum.PaymentPending, StatusEnum.PaymentPaid, StatusEnum.PaymentFailed, StatusEnum.OrderCancelled];
    private static readonly string[] OrderStatuses =
        [StatusEnum.OrderPending, StatusEnum.OrderPaid, StatusEnum.OrderCancelled, StatusEnum.OrderFailed, StatusEnum.OrderCompleted];
    private static readonly string[] TransactionStatuses = ["Success", "Failed"];
    private static readonly string[] Gateways = ["VNPay", "SePay"];

    private readonly ISystemBillingReadRepository _repository;

    public SystemBillingService(ISystemBillingReadRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResultDto<SystemBillingOrderListItemDto>> GetBillingOrdersAsync(
        SystemBillingOrderQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePaging(query.PageNumber, query.PageSize);
        ValidateRange(query.From, query.To);
        ValidateOptionalValue(query.PaymentStatus, PaymentStatuses, "PaymentStatus");
        ValidateOptionalValue(query.Status, OrderStatuses, "Status");
        if (!BillingSortFields.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("SortBy is not supported.", nameof(query.SortBy));
        ValidateDirection(query.SortDirection);
        return _repository.GetBillingOrdersAsync(query, cancellationToken);
    }

    public Task<SystemBillingOrderDetailDto?> GetBillingOrderAsync(
        Guid billingOrderId,
        CancellationToken cancellationToken = default)
        => _repository.GetBillingOrderAsync(billingOrderId, cancellationToken);

    public Task<PagedResultDto<SystemPaymentTransactionDto>> GetPaymentTransactionsAsync(
        SystemPaymentTransactionQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePaging(query.PageNumber, query.PageSize);
        ValidateRange(query.From, query.To);
        ValidateOptionalValue(query.Gateway, Gateways, "Gateway");
        ValidateOptionalValue(query.Status, TransactionStatuses, "Status");
        return _repository.GetPaymentTransactionsAsync(query, cancellationToken);
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static void ValidateRange(DateTime? from, DateTime? to)
    {
        if (from > to)
            throw new ArgumentException("From must not be later than To.");
    }

    private static void ValidateDirection(string direction)
    {
        if (!string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SortDirection must be 'asc' or 'desc'.", nameof(direction));
    }

    private static void ValidateOptionalValue(
        string? value,
        IReadOnlyCollection<string> allowedValues,
        string parameterName)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !allowedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"{parameterName} is not supported.", parameterName);
    }
}
