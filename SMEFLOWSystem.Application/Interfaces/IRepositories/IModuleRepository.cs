using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IModuleRepository
{
    Task<List<Module>> GetByIdsAsync(IEnumerable<int> ids);
    Task<List<Module>> GetAllActiveAsync();
    Task<List<Module>> GetAllAsync();
    Task AddAsync(Module module);
    Task<bool> ExistsByCodeOrShortCodeAsync(string code, string shortCode);
    Task<Module?> GetByCodeAsync(string code);
}
