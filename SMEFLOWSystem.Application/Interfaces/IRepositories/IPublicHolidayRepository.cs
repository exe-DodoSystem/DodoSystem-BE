using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories;

public interface IPublicHolidayRepository
{
    Task AddAsync(PublicHoliday holiday);
    Task<List<PublicHoliday>> GetAllAsync(Guid tenantId);
    Task<PublicHoliday?> GetByIdAsync(Guid id);
    Task DeleteAsync(PublicHoliday holiday);
    Task<bool> IsPublicHolidayAsync(Guid tenantId, DateOnly date);
}
