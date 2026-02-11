using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Helpers;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Services;

public class BillingOrderService : IBillingOrderService
{
    private readonly IBillingOrderRepository _billingOrderRepo;
    private readonly IModuleRepository _moduleRepo;
    private readonly IBillingOrderModuleRepository _billingOrderModuleRepo;

    public BillingOrderService(
        IBillingOrderRepository billingOrderRepo,
        IModuleRepository moduleRepo,
        IBillingOrderModuleRepository billingOrderModuleRepo)
    {
        _billingOrderRepo = billingOrderRepo;
        _moduleRepo = moduleRepo;
        _billingOrderModuleRepo = billingOrderModuleRepo;
    }

    public async Task<BillingOrder> CreateModuleBillingOrderAsync(
        Guid tenantId,
        Guid customerId,
        IReadOnlyCollection<int> moduleIds,
        bool isTrialOrder = false,
        DateTime? prorateUntilUtc = null)
    {
        if (moduleIds == null || moduleIds.Count == 0)
            throw new Exception("Vui lòng chọn ít nhất 1 module!");

        var modules = await _moduleRepo.GetByIdsAsync(moduleIds);
        if (modules.Count != moduleIds.Distinct().Count())
            throw new Exception("Có module không tồn tại hoặc đang bị tắt!");

        var now = DateTime.UtcNow;
        var lines = modules.Select(m =>
        {
            var lineTotal = m.MonthlyPrice;

            if (isTrialOrder)
            {
                lineTotal = 0m;
            }
            else if (prorateUntilUtc.HasValue)
            {
                var remainingDays = (int)Math.Floor((prorateUntilUtc.Value.Date - now.Date).TotalDays);
                if (remainingDays < 0) remainingDays = 0;

                // prorata = (monthlyPrice / 30) * remainingDays, floor to VND
                lineTotal = decimal.Floor((m.MonthlyPrice / 30m) * remainingDays);
            }

            return new BillingOrderModule
            {
                Id = Guid.NewGuid(),
                ModuleId = m.Id,
                Quantity = 1,
                UnitPrice = m.MonthlyPrice,
                LineTotal = lineTotal,
                CreatedAt = now
            };
        }).ToList();

        var total = lines.Sum(l => l.LineTotal);
        var discountPercent = isTrialOrder ? 0m : GetDiscountPercent(modules.Count);
        var discountAmount = decimal.Floor(total * discountPercent);
        var discountDecimal = discountAmount;

        var billingOrder = new BillingOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            BillingOrderNumber = AuthHelper.GenerateOrderNumber(),
            BillingDate = now,
            Status = StatusEnum.OrderPending,
            PaymentStatus = StatusEnum.PaymentPending,
            TotalAmount = total,
            DiscountAmount = discountDecimal,
            Notes = isTrialOrder
                ? "TRIAL"
                : (prorateUntilUtc.HasValue ? $"PRORATE_UNTIL:{prorateUntilUtc.Value:yyyy-MM-dd}" : null),
            CreatedAt = now
        };

        await _billingOrderRepo.AddAsync(billingOrder);

        foreach (var line in lines)
        {
            line.BillingOrderId = billingOrder.Id;
        }

        await _billingOrderModuleRepo.AddRangeAsync(lines);
        return billingOrder;
    }

    private static decimal GetDiscountPercent(int moduleCount)
    {
        if (moduleCount >= 4) return 0.20m;
        if (moduleCount >= 3) return 0.15m;
        if (moduleCount >= 2) return 0.10m;
        return 0m;
    }
}
