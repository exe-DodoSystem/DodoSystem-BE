using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class AttendanceSettingRepository : IAttendanceSettingRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public AttendanceSettingRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task<TenantAttendanceSetting?> GetByTenantIdAsync(Guid tenantId)
        {
            return await _context.TenantAttendanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);
        }

        public async Task UpsertAsync(TenantAttendanceSetting setting)
        {
            var existing = await _context.TenantAttendanceSettings
                .FirstOrDefaultAsync(x => x.TenantId == setting.TenantId);
            if (existing == null)
                await _context.TenantAttendanceSettings.AddAsync(setting);
            else
            {
                existing.Latitude = setting.Latitude;
                existing.Longitude = setting.Longitude;
                existing.CheckInRadiusMeters = setting.CheckInRadiusMeters;
                existing.WorkStartTime = setting.WorkStartTime;
                existing.WorkEndTime = setting.WorkEndTime;
                existing.LateThresholdMinutes = setting.LateThresholdMinutes;
                existing.EarlyLeaveThresholdMinutes = setting.EarlyLeaveThresholdMinutes;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }
}
