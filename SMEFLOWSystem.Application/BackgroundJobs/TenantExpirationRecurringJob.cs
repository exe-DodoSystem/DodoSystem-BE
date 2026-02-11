using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.BackgroundJobs;

public class TenantExpirationRecurringJob
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IModuleSubscriptionRepository _moduleSubscriptionRepo;
    private readonly IBillingOrderService _billingOrderService;
    private readonly IBillingService _billingService;
    private readonly ITransaction _transaction;

    public TenantExpirationRecurringJob(
        ITenantRepository tenantRepo,
        IUserRepository userRepo,
        ICustomerRepository customerRepo,
        IModuleSubscriptionRepository moduleSubscriptionRepo,
        IBillingOrderService billingOrderService,
        IBillingService billingService,
        ITransaction transaction)
    {
        _tenantRepo = tenantRepo;
        _userRepo = userRepo;
        _customerRepo = customerRepo;
        _moduleSubscriptionRepo = moduleSubscriptionRepo;
        _billingOrderService = billingOrderService;
        _billingService = billingService;
        _transaction = transaction;
    }

    // Runs daily 00:00 VN time
    public async Task SuspendExpiredTenantsAndSendRenewalEmailsAsync()
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiredTenants = await _tenantRepo.GetExpiredTenantsIgnoreTenantAsync(todayUtc);

        foreach (var tenant in expiredTenants)
        {
            if (string.Equals(tenant.Status, StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase))
                continue;

            Guid createdOrderId = Guid.Empty;
            string? ownerEmail = null;
            string tenantName = tenant.Name;

            await _transaction.ExecuteAsync(async () =>
            {
                var freshTenant = await _tenantRepo.GetByIdIgnoreTenantAsync(tenant.Id);
                if (freshTenant == null) return;

                if (!freshTenant.SubscriptionEndDate.HasValue || freshTenant.SubscriptionEndDate.Value >= todayUtc)
                    return;

                freshTenant.Status = StatusEnum.TenantSuspended;
                await _tenantRepo.UpdateIgnoreTenantAsync(freshTenant);

                if (freshTenant.OwnerUserId.HasValue)
                {
                    var owner = await _userRepo.GetByIdIgnoreTenantAsync(freshTenant.OwnerUserId.Value);
                    ownerEmail = owner?.Email;
                }

                if (string.IsNullOrWhiteSpace(ownerEmail))
                    return;

                var internalCustomer = await _customerRepo.GetInternalCustomerIgnoreTenantAsync(freshTenant.Id);
                if (internalCustomer == null)
                {
                    internalCustomer = new Customer
                    {
                        Id = Guid.NewGuid(),
                        TenantId = freshTenant.Id,
                        Name = freshTenant.Name,
                        Email = ownerEmail!,
                        Type = "Internal",
                        CompanyName = freshTenant.Name,
                        Phone = string.Empty,
                        Address = string.Empty,
                        Notes = string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    await _customerRepo.AddAsync(internalCustomer);
                }

                var subs = await _moduleSubscriptionRepo.GetByTenantIgnoreTenantAsync(freshTenant.Id);
                var moduleIds = subs
                    .Where(s => !s.IsDeleted)
                    .Select(s => s.ModuleId)
                    .Distinct()
                    .ToList();

                if (moduleIds.Count == 0)
                    return;

                var order = await _billingOrderService.CreateModuleBillingOrderAsync(
                    freshTenant.Id,
                    internalCustomer.Id,
                    moduleIds,
                    isTrialOrder: false,
                    prorateUntilUtc: null);

                createdOrderId = order.Id;
            });

            if (createdOrderId != Guid.Empty && !string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _billingService.EnqueuePaymentLinkEmailAsync(createdOrderId, ownerEmail, tenantName);
            }
        }
    }
}
