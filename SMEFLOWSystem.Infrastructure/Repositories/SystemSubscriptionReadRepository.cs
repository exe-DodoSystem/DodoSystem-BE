using Microsoft.EntityFrameworkCore;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemSubscriptionReadRepository : ISystemSubscriptionReadRepository
{
    private readonly SMEFLOWSystemContext _context;

    public SystemSubscriptionReadRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<SystemSubscriptionDto>> GetPagedAsync(
        SystemSubscriptionQueryDto request,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var query =
            from subscription in _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            join module in _context.Modules.AsNoTracking()
                on subscription.ModuleId equals module.Id
            where !tenant.IsDeleted
                && tenant.Name != SystemTenantConstants.Name
                && (request.IncludeCancelled || !subscription.IsDeleted)
            select new { Subscription = subscription, Tenant = tenant, Module = module };

        if (!string.IsNullOrWhiteSpace(request.SearchTenant))
        {
            var pattern = $"%{request.SearchTenant.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Tenant.Name, pattern));
        }
        if (request.TenantId.HasValue)
            query = query.Where(x => x.Tenant.Id == request.TenantId.Value);
        if (request.ModuleId.HasValue)
            query = query.Where(x => x.Module.Id == request.ModuleId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Subscription.Status, status));
        }
        if (request.ExpiringFrom.HasValue)
            query = query.Where(x => x.Subscription.EndDate >= request.ExpiringFrom.Value);
        if (request.ExpiringTo.HasValue)
            query = query.Where(x => x.Subscription.EndDate <= request.ExpiringTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Subscription.EndDate)
            .ThenBy(x => x.Subscription.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new SystemSubscriptionDto
            {
                Id = x.Subscription.Id,
                TenantId = x.Tenant.Id,
                TenantName = x.Tenant.Name,
                ModuleId = x.Module.Id,
                ModuleCode = x.Module.Code,
                ModuleName = x.Module.Name,
                StartDate = x.Subscription.StartDate,
                EndDate = x.Subscription.EndDate,
                Status = x.Subscription.Status,
                IsDeleted = x.Subscription.IsDeleted,
                CreatedAt = x.Subscription.CreatedAt,
                UpdatedAt = x.Subscription.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            item.RemainingDays = (int)Math.Ceiling((item.EndDate - nowUtc).TotalDays);

        return new PagedResultDto<SystemSubscriptionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
