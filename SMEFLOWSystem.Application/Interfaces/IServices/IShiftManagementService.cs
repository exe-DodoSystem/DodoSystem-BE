using SharedKernel.DTOs;
using SMEFLOWSystem.Application.DTOs.ShiftDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IShiftManagementService
{
    Task<PagedResultDto<ShiftDto>> GetPagedAsync(ShiftQueryDto query);
    Task<ShiftDto> GetByIdAsync(Guid id);
    Task<ShiftDto> CreateAsync(ShiftCreateDto request);
    Task<ShiftDto> UpdateAsync(Guid id, ShiftCreateDto request);
    Task DeleteAsync(Guid id);
}
