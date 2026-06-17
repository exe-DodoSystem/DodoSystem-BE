using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.HRDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IHrEmployeeService
{
    Task<PagedResultDto<EmployeeDto>> GetPagedAsync(EmployeeQueryDto query);
    Task<EmployeeDto> GetByIdAsync(Guid id);
    Task<EmployeeDto> CreateAsync(EmployeeCreateDto request);
    Task<EmployeeDto> UpdateAsync(Guid id, EmployeeUpdateDto request);
    Task DeleteAsync(Guid id);
    Task<List<EmployeeDto>> GetAllByDepartmentId(Guid departmentId);

    /// <summary>[TenantAdmin, HRManager] Cập nhật lương cơ bản cho nhân viên</summary>
    Task<EmployeeDto> UpdateSalaryAsync(Guid employeeId, UpdateSalaryDto dto);
}
