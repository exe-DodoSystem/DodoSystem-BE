using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ManagerDepartmentRepository : IManagerDepartmentRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ManagerDepartmentRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task<List<ManagerDepartment>> GetByUserIdAsync(Guid userId)
    {
        return await _context.ManagerDepartments
            .Include(md => md.Department)
            .Where(md => md.UserId == userId)
            .ToListAsync();
    }

    public async Task<List<Guid>> GetDepartmentIdsByUserIdAsync(Guid userId)
    {
        return await _context.ManagerDepartments
            .Where(md => md.UserId == userId)
            .Select(md => md.DepartmentId)
            .ToListAsync();
    }

    public async Task AddAsync(ManagerDepartment entity)
    {
        await _context.ManagerDepartments.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid userId, Guid departmentId)
    {
        var entity = await _context.ManagerDepartments
            .FirstOrDefaultAsync(md => md.UserId == userId && md.DepartmentId == departmentId);

        if (entity != null)
        {
            _context.ManagerDepartments.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllByUserIdAsync(Guid userId)
    {
        var entities = await _context.ManagerDepartments
            .Where(md => md.UserId == userId)
            .ToListAsync();

        if (entities.Count > 0)
        {
            _context.ManagerDepartments.RemoveRange(entities);
            await _context.SaveChangesAsync();
        }
    }

    public Task<bool> ExistsAsync(Guid userId, Guid departmentId)
    {
        return _context.ManagerDepartments
            .AnyAsync(md => md.UserId == userId && md.DepartmentId == departmentId);
    }
}
