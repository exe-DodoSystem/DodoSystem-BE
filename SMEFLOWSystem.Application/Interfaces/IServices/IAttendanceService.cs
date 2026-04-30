using System;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IAttendanceService
{
    Task<RawPunchLogDto> SubmitPunchAsync(Guid userId, SubmitPunchRequestDto request);
    Task<TodayAttendanceDto> GetMyTodayStatusAsync(Guid userId);
    Task<List<MyAttendanceHistoryItemDto>> GetMyHistoryAsync(Guid userId, int month, int year);
    
    // Manual Punch & Recalculate
    Task<RawPunchLogDto> ManualPunchAsync(ManualPunchRequestDto request);
    Task RecalculateAttendanceAsync(Guid employeeId, DateOnly fromDate, DateOnly toDate);

    // Appeal Flow
    Task<TimesheetAppealDto> SubmitAppealAsync(Guid userId, SubmitAppealRequestDto request);
    Task<List<TimesheetAppealDto>> GetMyAppealsAsync(Guid userId);
    Task<TimesheetAppealDto> ProcessAppealAsync(Guid hrUserId, Guid appealId, ApproveAppealRequestDto request);
    Task<List<TimesheetAppealDto>> GetPendingAppealsAsync();

    // Settings
    Task<AttendanceSettingDto> GetSettingsAsync();
    Task<AttendanceSettingDto> UpdateSettingsAsync(UpdateAttendanceSettingRequestDto request);

    // Reports
    Task<List<HRMonthlyReportItemDto>> GetHRMonthlyReportAsync(int month, int year);
}
