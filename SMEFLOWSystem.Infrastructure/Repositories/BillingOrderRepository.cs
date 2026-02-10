using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class BillingOrderRepository : IBillingOrderRepository
{
    private readonly SMEFLOWSystemContext _context;

    public BillingOrderRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BillingOrder billingOrder)
    {
        await _context.BillingOrders.AddAsync(billingOrder);
        await _context.SaveChangesAsync();
    }

    public async Task<BillingOrder?> GetByIdAsync(Guid billingOrderId)
    {
        return await _context.BillingOrders.FindAsync(billingOrderId);
    }

    public async Task<BillingOrder?> GetByIdIgnoreTenantAsync(Guid billingOrderId)
    {
        return await _context.BillingOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == billingOrderId);
    }

    public async Task<BillingOrder?> UpdateAsync(BillingOrder billingOrder)
    {
        var existing = await _context.BillingOrders.FirstOrDefaultAsync(o => o.Id == billingOrder.Id);
        if (existing == null) return null;

        existing.TenantId = billingOrder.TenantId;
        existing.BillingOrderNumber = billingOrder.BillingOrderNumber;
        existing.CustomerId = billingOrder.CustomerId;
        existing.BillingDate = billingOrder.BillingDate;
        existing.TotalAmount = billingOrder.TotalAmount;
        existing.DiscountAmount = billingOrder.DiscountAmount;
        existing.FinalAmount = billingOrder.FinalAmount;
        existing.PaymentStatus = billingOrder.PaymentStatus;
        existing.Status = billingOrder.Status;
        existing.Notes = billingOrder.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<BillingOrder?> UpdateIgnoreTenantAsync(BillingOrder billingOrder)
    {
        var existing = await _context.BillingOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == billingOrder.Id);
        if (existing == null) return null;

        existing.TenantId = billingOrder.TenantId;
        existing.BillingOrderNumber = billingOrder.BillingOrderNumber;
        existing.CustomerId = billingOrder.CustomerId;
        existing.BillingDate = billingOrder.BillingDate;
        existing.TotalAmount = billingOrder.TotalAmount;
        existing.DiscountAmount = billingOrder.DiscountAmount;
        existing.FinalAmount = billingOrder.FinalAmount;
        existing.PaymentStatus = billingOrder.PaymentStatus;
        existing.Status = billingOrder.Status;
        existing.Notes = billingOrder.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }
}
