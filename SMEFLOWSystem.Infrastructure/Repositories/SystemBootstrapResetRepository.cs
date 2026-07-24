using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Common;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public sealed class SystemBootstrapResetRepository : ISystemBootstrapResetRepository
{
    private readonly SMEFLOWSystemContext _context;

    public SystemBootstrapResetRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public Task<SystemBootstrapResetTarget?> FindResetTargetAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return (
            from user in _context.Users.IgnoreQueryFilters().AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters().AsNoTracking()
                on user.TenantId equals tenant.Id
            join userRole in _context.UserRoles.IgnoreQueryFilters().AsNoTracking()
                on new { user.Id, user.TenantId }
                equals new { Id = userRole.UserId, userRole.TenantId }
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where user.Id == actorUserId
                && !user.IsDeleted
                && user.IsActive
                && !tenant.IsDeleted
                && tenant.Name == SystemTenantConstants.Name
                && tenant.OwnerUserId == actorUserId
                && role.Name == RoleConstants.SystemAdmin
            select new SystemBootstrapResetTarget(
                tenant.Id,
                user.Id,
                role.Id,
                user.PasswordHash))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SystemBootstrapDependencyCounts> GetDependencyCountsAsync(
        SystemBootstrapResetTarget target,
        CancellationToken cancellationToken)
    {
        var occupied = new List<string>();
        async Task CheckAsync(string name, Task<bool> query)
        {
            if (await query)
                occupied.Add(name);
        }

        var tenantId = target.TenantId;
        await CheckAsync("Users", _context.Users.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id != target.UserId, cancellationToken));
        await CheckAsync("UserRoles", _context.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId
                && (x.UserId != target.UserId || x.RoleId != target.SystemAdminRoleId), cancellationToken));
        await CheckAsync("RefreshTokens", _context.RefreshTokens.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.UserId != target.UserId, cancellationToken));
        await CheckAsync("Customers", _context.Customers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Departments", _context.Departments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Employees", _context.Employees.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Invites", _context.Invites.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("BillingOrders", _context.BillingOrders.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("BillingOrderModules", _context.BillingOrderModules.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ModuleSubscriptions", _context.ModuleSubscriptions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Orders", _context.Orders.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("OrderItems", _context.OrderItems.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("PaymentTransactions", _context.PaymentTransactions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Payrolls", _context.Payrolls.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Positions", _context.Positions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("TenantAttendanceSettings", _context.TenantAttendanceSettings.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Notifications", _context.Notifications.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("Shifts", _context.Shifts.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ShiftSegments", _context.ShiftSegments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ShiftPatterns", _context.ShiftPatterns.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ShiftPatternDays", _context.ShiftPatternDays.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("EmployeeShiftPatterns", _context.EmployeeShiftPatterns.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("OvertimeRequests", _context.OvertimeRequests.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("DailyTimesheets", _context.DailyTimesheets.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("DailyTimesheetSegments", _context.DailyTimesheetSegments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("DailyTimesheetAuditLogs", _context.DailyTimesheetAuditLogs.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("TimesheetPeriods", _context.TimesheetPeriods.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("LeaveRequests", _context.LeaveRequests.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("LeaveRequestSegments", _context.LeaveRequestSegments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("LeaveTypes", _context.LeaveTypes.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("EmployeeLeaveBalances", _context.EmployeeLeaveBalances.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("RawPunchLogs", _context.RawPunchLogs.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ManagerDepartments", _context.ManagerDepartments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("PublicHolidays", _context.PublicHolidays.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("TimesheetAppeals", _context.TimesheetAppeals.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("ManualMonthlyTimesheets", _context.ManualMonthlyTimesheets.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("EmployeeSalaryHistories", _context.EmployeeSalaryHistories.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));
        await CheckAsync("EmployeeBonusDeductionEntries", _context.EmployeeBonusDeductionEntries.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken));

        return new SystemBootstrapDependencyCounts { OccupiedResources = occupied };
    }

    public async Task DeleteBootstrapIdentityAsync(
        SystemBootstrapResetTarget target,
        CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants.IgnoreQueryFilters()
            .SingleAsync(x => x.Id == target.TenantId, cancellationToken);
        var user = await _context.Users.IgnoreQueryFilters()
            .SingleAsync(x => x.Id == target.UserId, cancellationToken);
        var refreshTokens = await _context.RefreshTokens.IgnoreQueryFilters()
            .Where(x => x.TenantId == target.TenantId && x.UserId == target.UserId)
            .ToListAsync(cancellationToken);
        var userRoles = await _context.UserRoles.IgnoreQueryFilters()
            .Where(x => x.TenantId == target.TenantId
                && x.UserId == target.UserId
                && x.RoleId == target.SystemAdminRoleId)
            .ToListAsync(cancellationToken);

        _context.RefreshTokens.RemoveRange(refreshTokens);
        _context.UserRoles.RemoveRange(userRoles);
        tenant.OwnerUserId = null;
        await _context.SaveChangesAsync(cancellationToken);

        _context.Users.Remove(user);
        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsActiveSystemAdminAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await FindResetTargetAsync(userId, cancellationToken) != null;
}
