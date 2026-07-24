using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.SystemDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemTenantReadRepository : ISystemTenantReadRepository
{
    private readonly SMEFLOWSystemContext _context;

    public SystemTenantReadRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<SystemTenantListItemDto>> GetPagedAsync(
        SystemTenantQueryDto request,
        DateOnly today,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var query = _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Name != SystemTenantConstants.Name);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeStatus(request.Status);
            query = query.Where(t => t.Status == status);
        }

        if (request.ModuleId.HasValue)
        {
            query = query.Where(t => _context.ModuleSubscriptions
                .IgnoreQueryFilters()
                .Any(s => s.TenantId == t.Id
                    && s.ModuleId == request.ModuleId.Value
                    && !s.IsDeleted));
        }

        if (request.ExpiringInDays.HasValue)
        {
            var endDate = today.AddDays(request.ExpiringInDays.Value);
            query = query.Where(t => t.SubscriptionEndDate.HasValue
                && t.SubscriptionEndDate.Value >= today
                && t.SubscriptionEndDate.Value <= endDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var users = _context.Users.IgnoreQueryFilters().AsNoTracking();
        var subscriptions = _context.ModuleSubscriptions.IgnoreQueryFilters().AsNoTracking();
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new SystemTenantListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                SubscriptionEndDate = t.SubscriptionEndDate,
                ActiveModuleCount = subscriptions
                    .Where(s => s.TenantId == t.Id
                        && !s.IsDeleted
                        && (s.Status == StatusEnum.ModuleActive || s.Status == StatusEnum.ModuleTrial)
                        && s.StartDate <= nowUtc
                        && s.EndDate > nowUtc)
                    .Select(s => s.ModuleId)
                    .Distinct()
                    .Count(),
                UserCount = users.Count(u => u.TenantId == t.Id && !u.IsDeleted),
                OwnerUserId = t.OwnerUserId,
                OwnerName = users
                    .Where(u => u.Id == t.OwnerUserId && !u.IsDeleted)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                OwnerEmail = users
                    .Where(u => u.Id == t.OwnerUserId && !u.IsDeleted)
                    .Select(u => u.Email)
                    .FirstOrDefault(),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.RemainingDays = item.SubscriptionEndDate.HasValue
                ? item.SubscriptionEndDate.Value.DayNumber - today.DayNumber
                : null;
        }

        return new PagedResultDto<SystemTenantListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<SystemTenantDetailDto?> GetByIdAsync(
        Guid tenantId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var users = _context.Users.IgnoreQueryFilters().AsNoTracking();
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tenantId
                && !t.IsDeleted
                && t.Name != SystemTenantConstants.Name)
            .Select(t => new SystemTenantDetailDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                SubscriptionEndDate = t.SubscriptionEndDate,
                UserCount = users.Count(u => u.TenantId == t.Id && !u.IsDeleted),
                Owner = users
                    .Where(u => u.Id == t.OwnerUserId && !u.IsDeleted)
                    .Select(u => new SystemTenantOwnerDto
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        IsActive = u.IsActive,
                        IsVerified = u.IsVerified
                    })
                    .FirstOrDefault(),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant == null)
            return null;

        tenant.RemainingDays = tenant.SubscriptionEndDate.HasValue
            ? tenant.SubscriptionEndDate.Value.DayNumber - today.DayNumber
            : null;

        tenant.Modules = await _context.ModuleSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.Module!.Name)
            .Select(s => new SystemTenantModuleDto
            {
                ModuleId = s.ModuleId,
                ModuleName = s.Module != null ? s.Module.Name : string.Empty,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Status = s.Status
            })
            .ToListAsync(cancellationToken);

        return tenant;
    }

    public async Task<PagedResultDto<SystemTenantUserDto>?> GetUsersAsync(
        Guid tenantId,
        SystemTenantUserQueryDto request,
        CancellationToken cancellationToken)
    {
        var tenantExists = await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId
                && !t.IsDeleted
                && t.Name != SystemTenantConstants.Name,
                cancellationToken);
        if (!tenantExists)
            return null;

        var query = _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, pattern)
                || EF.Functions.ILike(u.Email, pattern));
        }

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleName = request.Role.Trim();
            query = query.Where(u => _context.UserRoles
                .IgnoreQueryFilters()
                .Any(ur => ur.TenantId == tenantId
                    && ur.UserId == u.Id
                    && EF.Functions.ILike(ur.Role.Name, roleName)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new SystemTenantUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                IsVerified = u.IsVerified,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var userIds = items.Select(x => x.Id).ToList();
        var rolePairs = await _context.UserRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(ur => ur.TenantId == tenantId && userIds.Contains(ur.UserId))
            .Select(ur => new { ur.UserId, ur.Role.Name })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var rolesByUser = rolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(x => x.Name).ToList());

        foreach (var item in items)
            item.Roles = rolesByUser.GetValueOrDefault(item.Id) ?? Array.Empty<string>();

        return new PagedResultDto<SystemTenantUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    private static IQueryable<Tenant> ApplySorting(
        IQueryable<Tenant> query,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            "status" => descending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "subscriptionenddate" => descending
                ? query.OrderByDescending(t => t.SubscriptionEndDate)
                : query.OrderBy(t => t.SubscriptionEndDate),
            _ => descending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
        };
    }

    private static string NormalizeStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "active" => StatusEnum.TenantActive,
            "trial" => StatusEnum.TenantTrial,
            "pendingpayment" => StatusEnum.TenantPending,
            "suspended" => StatusEnum.TenantSuspended,
            _ => status.Trim()
        };
}
