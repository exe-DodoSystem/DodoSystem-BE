using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IShiftRepository
{
    Task<(List<Shift> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool includeDeleted,
        int pageNumber,
        int pageSize);

    Task<Shift?> GetByIdWithSegmentsAsync(Guid id);
    Task AddAsync(Shift shift);
    Task<Shift> UpdateAsync(Shift shift);
    Task SoftDeleteAsync(Shift shift);
    Task<bool> HasUsageAsync(Guid shiftId);
}
