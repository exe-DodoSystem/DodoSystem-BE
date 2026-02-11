using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class ModuleRepository : IModuleRepository
{
    private readonly SMEFLOWSystemContext _context;

    public ModuleRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public Task<List<Module>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        return _context.Modules.Where(m => idList.Contains(m.Id) && m.IsActive).ToListAsync();
    }

    public Task<List<Module>> GetAllActiveAsync()
        => _context.Modules.Where(m => m.IsActive).OrderBy(m => m.Id).ToListAsync();

    public Task<List<Module>> GetAllAsync()
        => _context.Modules.OrderBy(m => m.Id).ToListAsync();

    public async Task AddAsync(Module module)
    {
        await _context.Modules.AddAsync(module);
        await _context.SaveChangesAsync();
    }

    public Task<bool> ExistsByCodeOrShortCodeAsync(string code, string shortCode)
    {
        var normalizedCode = code.Trim();
        var normalizedShortCode = shortCode.Trim();

        return _context.Modules.AnyAsync(m => m.Code == normalizedCode || m.ShortCode == normalizedShortCode);
    }

    public Task<Module?> GetByCodeAsync(string code)
    {
        var normalized = code.Trim();
        return _context.Modules.FirstOrDefaultAsync(m => m.Code == normalized && m.IsActive);
    }
}
