using Microsoft.EntityFrameworkCore;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemBillingReadRepository : ISystemBillingReadRepository
{
    private readonly SMEFLOWSystemContext _context;

    public SystemBillingReadRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<SystemBillingOrderListItemDto>> GetBillingOrdersAsync(
        SystemBillingOrderQueryDto request,
        CancellationToken cancellationToken)
    {
        var query =
            from order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new BillingOrderRow
            {
                Id = order.Id,
                BillingOrderNumber = order.BillingOrderNumber,
                TenantId = order.TenantId,
                TenantName = tenant.Name,
                BillingDate = order.BillingDate,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                PaymentStatus = order.PaymentStatus,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.BillingOrderNumber, pattern)
                || EF.Functions.ILike(x.TenantName, pattern));
        }
        if (request.TenantId.HasValue)
            query = query.Where(x => x.TenantId == request.TenantId.Value);
        if (!string.IsNullOrWhiteSpace(request.PaymentStatus))
        {
            var status = request.PaymentStatus.Trim();
            query = query.Where(x => EF.Functions.ILike(x.PaymentStatus, status));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Status, status));
        }
        if (request.From.HasValue)
            query = query.Where(x => x.BillingDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(x => x.BillingDate <= request.To.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplyBillingSort(query, request.SortBy, request.SortDirection);
        var modules = _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking();
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new SystemBillingOrderListItemDto
            {
                Id = x.Id,
                BillingOrderNumber = x.BillingOrderNumber,
                TenantId = x.TenantId,
                TenantName = x.TenantName,
                BillingDate = x.BillingDate,
                TotalAmount = x.TotalAmount,
                DiscountAmount = x.DiscountAmount ?? 0,
                FinalAmount = x.FinalAmount
                    ?? (x.TotalAmount - (x.DiscountAmount ?? 0)),
                PaymentStatus = x.PaymentStatus,
                Status = x.Status,
                ModuleCount = modules
                    .Where(line => line.BillingOrderId == x.Id)
                    .Select(line => line.ModuleId)
                    .Distinct()
                    .Count(),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<SystemBillingOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<SystemBillingOrderDetailDto?> GetBillingOrderAsync(
        Guid billingOrderId,
        CancellationToken cancellationToken)
    {
        var detail = await (
            from order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.Id == billingOrderId
                && order.IsDeleted != true
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new SystemBillingOrderDetailDto
            {
                Id = order.Id,
                BillingOrderNumber = order.BillingOrderNumber,
                TenantId = order.TenantId,
                TenantName = tenant.Name,
                BillingDate = order.BillingDate,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount ?? 0,
                FinalAmount = order.FinalAmount
                    ?? (order.TotalAmount - (order.DiscountAmount ?? 0)),
                PaymentStatus = order.PaymentStatus,
                Status = order.Status,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (detail == null)
            return null;

        detail.Modules = await (
            from line in _context.BillingOrderModules.IgnoreQueryFilters().AsNoTracking()
            join module in _context.Modules.AsNoTracking()
                on line.ModuleId equals module.Id
            where line.BillingOrderId == billingOrderId
            orderby module.Name
            select new SystemBillingOrderModuleLineDto
            {
                Id = line.Id,
                ModuleId = module.Id,
                ModuleCode = module.Code,
                ModuleName = module.Name,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                ProrationDays = line.ProrationDays,
                LineTotal = line.LineTotal,
                CreatedAt = line.CreatedAt
            })
            .ToListAsync(cancellationToken);

        detail.PaymentTransactions = await ProjectPaymentTransactions()
            .Where(x => x.BillingOrderId == billingOrderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return detail;
    }

    public async Task<PagedResultDto<SystemPaymentTransactionDto>> GetPaymentTransactionsAsync(
        SystemPaymentTransactionQueryDto request,
        CancellationToken cancellationToken)
    {
        var query = ProjectPaymentTransactions();
        if (request.TenantId.HasValue)
            query = query.Where(x => x.TenantId == request.TenantId.Value);
        if (request.BillingOrderId.HasValue)
            query = query.Where(x => x.BillingOrderId == request.BillingOrderId.Value);
        if (!string.IsNullOrWhiteSpace(request.Gateway))
        {
            var gateway = request.Gateway.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Gateway, gateway));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Status, status));
        }
        if (request.From.HasValue)
            query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(x => x.CreatedAt <= request.To.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<SystemPaymentTransactionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    private IQueryable<SystemPaymentTransactionDto> ProjectPaymentTransactions()
        =>
            from payment in _context.PaymentTransactions.IgnoreQueryFilters().AsNoTracking()
            join order in _context.BillingOrders.IgnoreQueryFilters().AsNoTracking()
                on payment.BillingOrderId equals order.Id
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on order.TenantId equals tenant.Id
            where order.IsDeleted != true
                && payment.TenantId == order.TenantId
                && !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
            select new SystemPaymentTransactionDto
            {
                Id = payment.Id,
                TenantId = payment.TenantId,
                TenantName = tenant.Name,
                BillingOrderId = payment.BillingOrderId,
                BillingOrderNumber = order.BillingOrderNumber,
                Gateway = payment.Gateway,
                GatewayTransactionId = payment.GatewayTransactionId,
                GatewayResponseCode = payment.GatewayResponseCode,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt,
                ProcessedAt = payment.ProcessedAt
            };

    private static IQueryable<BillingOrderRow> ApplyBillingSort(
        IQueryable<BillingOrderRow> query,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "createdat" => descending
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt),
            "finalamount" => descending
                ? query.OrderByDescending(x => x.FinalAmount)
                : query.OrderBy(x => x.FinalAmount),
            "billingordernumber" => descending
                ? query.OrderByDescending(x => x.BillingOrderNumber)
                : query.OrderBy(x => x.BillingOrderNumber),
            _ => descending
                ? query.OrderByDescending(x => x.BillingDate)
                : query.OrderBy(x => x.BillingDate)
        };
    }

    private sealed class BillingOrderRow
    {
        public Guid Id { get; set; }
        public string BillingOrderNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public DateTime BillingDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
