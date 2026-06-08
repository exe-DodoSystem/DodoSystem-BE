using SMEFLOWSystem.Application.DTOs.HRDtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IManualTimesheetService
{
    Task<ManualMonthlyTimesheetDto> UpsertAsync(Guid tenantId, ManualMonthlyTimesheetUpsertDto dto);
    Task<List<ManualMonthlyTimesheetDto>> GetByMonthAsync(Guid tenantId, int month, int year);
    Task<bool> DeleteAsync(Guid tenantId, Guid id);
}
