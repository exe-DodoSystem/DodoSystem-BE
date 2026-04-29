using Microsoft.Extensions.Logging;
using SMEFLOWSystem.Application.Interfaces.IServices;

namespace SMEFLOWSystem.Application.BackgroundJobs;

public class AttendanceResolutionRecurringJob
{
    private readonly IAttendanceResolutionService _attendanceResolutionService;
    private readonly ILogger<AttendanceResolutionRecurringJob> _logger;

    public AttendanceResolutionRecurringJob(
        IAttendanceResolutionService attendanceResolutionService,
        ILogger<AttendanceResolutionRecurringJob> logger)
    {
        _attendanceResolutionService = attendanceResolutionService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Attendance resolution recurring job triggered.");

        await _attendanceResolutionService.ProcessUnresolvedPunchesAsync();

        _logger.LogInformation("Attendance resolution recurring job completed.");
    }
}