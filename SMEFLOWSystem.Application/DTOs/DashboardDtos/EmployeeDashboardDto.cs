using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class EmployeeDashboardDto
{
    public TodayAttendanceDto? MyTodayStatus { get; set; }       // Reuse TodayAttendanceDto từ AttendanceDtos
    public MyMonthSummaryDto MyMonthSummary { get; set; } = new();
    public CurrentShiftDto? MyCurrentShift { get; set; }
    public PayrollDto? MyLatestPayroll { get; set; }      // Reuse PayrollDto từ PayrollDtos
    public int MyPendingAppealsCount { get; set; }
}
