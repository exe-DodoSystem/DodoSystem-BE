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
        private readonly IEmployeeRepository _employeeRepository;

        public AttendanceService(IRawPunchLogRepository punchLogRepo, IEmployeeRepository employeeRepository)
        {
            _punchLogRepo = punchLogRepo;
            _employeeRepository = employeeRepository;
        }

        public async Task<RawPunchLogDto> SubmitPunchAsync(Guid userId, SubmitPunchRequestDto request)
        {
            var employee = await _employeeRepository.GetByUserIdAsync(userId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found for current user.");
            }

            var punch = new RawPunchLog()
            {
                EmployeeId = employee.Id,
                Timestamp = DateTime.UtcNow,
                DeviceId = request.DeviceId,
                PunchType = request.PunchType ?? "Auto",
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                SelfieUrl = request.SelfieUrl,
                IsProcessed = false 
            };

            await _punchLogRepo.AddAsync(punch);

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
