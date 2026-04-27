using System;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IRawPunchLogRepository _punchLogRepo;

        public AttendanceService(IRawPunchLogRepository punchLogRepo)
        {
            _punchLogRepo = punchLogRepo;
        }

        public async Task<RawPunchLogDto> SubmitPunchAsync(Guid employeeId, SubmitPunchRequestDto request)
        {
            // 1. Create a RawPunchLog from the request
            var punch = new RawPunchLog()
            {
                EmployeeId = employeeId,
                Timestamp = DateTime.UtcNow,
                DeviceId = request.DeviceId,
                PunchType = request.PunchType ?? "Auto",
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                SelfieUrl = request.SelfieUrl,
                IsProcessed = false // Needs to be resolved by the engine later
            };

            // 2. Save it to the repository
            await _punchLogRepo.AddAsync(punch);

            // 3. Return to client
            return new RawPunchLogDto
            {
                Id = punch.Id,
                EmployeeId = punch.EmployeeId,
                Timestamp = punch.Timestamp,
                DeviceId = punch.DeviceId,
                IsProcessed = punch.IsProcessed,
                PunchType = punch.PunchType
            };
        }
    }
}
