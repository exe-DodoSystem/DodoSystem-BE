using System;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IAttendanceService
{
    Task<RawPunchLogDto> SubmitPunchAsync(Guid employeeId, SubmitPunchRequestDto request);
}
