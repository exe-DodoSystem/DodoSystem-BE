using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee?> GetByUserIdAsync(Guid userId);
    Task<List<Employee>> GetAllActiveEmployeeByTenantId(Guid tenantId);
    Task<List<Employee>> GetByIdsAsync(List<Guid> employeeIds);
    Task AddAsync(Employee employee);
    Task<Employee> UpdateAsync(Employee employee);
    Task SoftDeleteResignedAsync(Employee employee);
    Task<List<Employee>> GetByDepartmentIdAsync(Guid departmentId);

    Task<(List<Employee> Items, int TotalCount)> GetPagedAsync(
        Guid? departmentId,
        Guid? positionId,
        int? roleId,
        string? status,
        bool includeResigned,
        string? search,
        int pageNumber,
        int pageSize,
        string? sortBy,
        string? sortDir);
}
