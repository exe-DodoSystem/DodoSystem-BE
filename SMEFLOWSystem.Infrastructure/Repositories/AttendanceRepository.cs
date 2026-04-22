using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly SMEFLOWSystemContext _context;

    public AttendanceRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Attendance attendance)
    {
        await _context.Attendances.AddAsync(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task<Attendance?> GetByIdAsync(Guid id)
    {
        return await _context.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Department)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Attendance?> GetTodayByEmployeeIdAsync(Guid employeeId, DateOnly workDate)
    {
        return await _context.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate);
    }

    public async Task<List<Attendance>> GetByEmployeeIdAsync(Guid employeeId, DateOnly fromDate, DateOnly toDate)
    {
        return await _context.Attendances
            .Where(a => a.EmployeeId == employeeId
                     && a.WorkDate >= fromDate
                     && a.WorkDate <= toDate)
            .OrderByDescending(a => a.WorkDate)
            .ToListAsync();
    }

    public async Task<(List<Attendance> Items, int TotalCount)> GetPagedAsync(
        Guid? departmentId,
        Guid? employeeId,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? status,
        string? approvalStatus,
        string? search,
        int pageNumber,
        int pageSize,
        string? sortBy,
        string? sortDir)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Department)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(a => a.Employee != null && a.Employee.DepartmentId == departmentId.Value);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (fromDate.HasValue)
            query = query.Where(a => a.WorkDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.WorkDate <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(approvalStatus))
            query = query.Where(a => a.ApprovalStatus == approvalStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Employee != null && a.Employee.FullName.Contains(s));
        }

        var total = await query.CountAsync();

        var asc = !string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "checkintime"   => asc ? query.OrderBy(a => a.CheckInTime)   : query.OrderByDescending(a => a.CheckInTime),
            "lateminutes"   => asc ? query.OrderBy(a => a.LateMinutes)   : query.OrderByDescending(a => a.LateMinutes),
            "status"        => asc ? query.OrderBy(a => a.Status)        : query.OrderByDescending(a => a.Status),
            _               => asc ? query.OrderBy(a => a.WorkDate)      : query.OrderByDescending(a => a.WorkDate),
        };

        var skip = (pageNumber - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync();

        return (items, total);
    }

    public async Task UpdateAsync(Attendance attendance)
    {
        var existing = await _context.Attendances.FirstOrDefaultAsync(a => a.Id == attendance.Id);
        if (existing == null) return;

        existing.CheckInTime = attendance.CheckInTime;
        existing.CheckOutTime = attendance.CheckOutTime;
        existing.CheckInLatitude = attendance.CheckInLatitude;
        existing.CheckInLongitude = attendance.CheckInLongitude;
        existing.CheckOutLatitude = attendance.CheckOutLatitude;
        existing.CheckOutLongitude = attendance.CheckOutLongitude;
        existing.CheckInSelfieUrl = attendance.CheckInSelfieUrl;
        existing.CheckOutSelfieUrl = attendance.CheckOutSelfieUrl;
        existing.Status = attendance.Status;
        existing.LateMinutes = attendance.LateMinutes;
        existing.EarlyLeaveMinutes = attendance.EarlyLeaveMinutes;
        existing.ApprovalStatus = attendance.ApprovalStatus;
        existing.ApprovedByUserId = attendance.ApprovedByUserId;
        existing.ApprovalNotes = attendance.ApprovalNotes;
        existing.ApprovedAt = attendance.ApprovedAt;
        existing.Notes = attendance.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.Attendances.Update(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountWorkingDaysAsync(Guid employeeId, int month, int year)
    {
        return await _context.Attendances
            .Where(a => a.EmployeeId == employeeId
                     && a.WorkDate.Month == month
                     && a.WorkDate.Year == year
                     && a.Status != StatusEnum.AttendanceAbsent
                     && a.CheckInTime != null)
            .CountAsync();
    }

    public async Task<List<Attendance>> GetByEmployeeMonthAsync(Guid employeeId, int month, int year)
    {
        return await _context.Attendances
            .Where(a => a.EmployeeId == employeeId
                     && a.WorkDate.Month == month
                     && a.WorkDate.Year == year)
            .OrderBy(a => a.WorkDate)
            .ToListAsync();
    }

    public async Task<List<Attendance>> GetByTenantMonthAsync(Guid tenantId, int month, int year)
    {
        return await _context.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Department)
            .Where(a => a.TenantId == tenantId
                     && a.WorkDate.Month == month
                     && a.WorkDate.Year == year)
            .OrderBy(a => a.Employee.FullName)
            .ThenBy(a => a.WorkDate)
            .ToListAsync();
    }
}
