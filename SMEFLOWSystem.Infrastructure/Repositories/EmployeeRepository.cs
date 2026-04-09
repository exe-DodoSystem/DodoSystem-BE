using Microsoft.EntityFrameworkCore;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly SMEFLOWSystemContext _context;

    public EmployeeRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public Task<Employee?> GetByIdAsync(Guid id)
    {
        return _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public Task<Employee?> GetByUserIdAsync(Guid userId)
    {
        return _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.UserId == userId);
    }

    public async Task AddAsync(Employee employee)
    {
        await _context.Employees.AddAsync(employee);
        await _context.SaveChangesAsync();
    }

    public async Task<Employee> UpdateAsync(Employee employee)
    {
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
        return employee;
    }

    public async Task SoftDeleteResignedAsync(Employee employee)
    {
        employee.IsDeleted = true;
        employee.UpdatedAt = DateTime.UtcNow;
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<Employee> Items, int TotalCount)> GetPagedAsync(
        Guid? departmentId,
        Guid? positionId,
        string? status,
        bool includeResigned,
        string? search,
        int pageNumber,
        int pageSize,
        string? sortBy,
        string? sortDir)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        if (positionId.HasValue)
            query = query.Where(e => e.PositionId == positionId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);
        if (!includeResigned)
            query = query.Where(e => e.Status != StatusEnum.EmployeeResigned);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.FullName.Contains(s) || e.Email.Contains(s) || e.Phone.Contains(s));
        }

        var total = await query.CountAsync();

        var asc = !string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "fullname" => asc ? query.OrderBy(e => e.FullName) : query.OrderByDescending(e => e.FullName),
            "hiredate" => asc ? query.OrderBy(e => e.HireDate) : query.OrderByDescending(e => e.HireDate),
            "basesalary" => asc ? query.OrderBy(e => e.BaseSalary) : query.OrderByDescending(e => e.BaseSalary),
            "status" => asc ? query.OrderBy(e => e.Status) : query.OrderByDescending(e => e.Status),
            _ => asc ? query.OrderBy(e => e.FullName) : query.OrderByDescending(e => e.FullName),
        };

        var skip = (pageNumber - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync();
        return (items, total);
    }
}
