using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IAttendanceSettingRepository
    {
        Task<TenantAttendanceSetting?> GetByTenantIdAsync(Guid tenantId);
        Task UpsertAsync(TenantAttendanceSetting setting);
    }
}
