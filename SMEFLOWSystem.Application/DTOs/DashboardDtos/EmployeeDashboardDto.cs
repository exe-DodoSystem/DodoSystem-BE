using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.DTOs.PayrollDtos;

using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class EmployeeDashboardDto
{
    public TodayAttendanceDto? MyTodayStatus { get; set; }       // Reuse TodayAttendanceDto từ AttendanceDtos
    public MyMonthSummaryDto? MyMonthSummary { get; set; }
    public CurrentShiftDto? MyCurrentShift { get; set; }
    public PayrollDto? MyLatestPayroll { get; set; }      // Reuse PayrollDto từ PayrollDtos
    public int? MyPendingAppealsCount { get; set; }
    public List<string> AvailableModules { get; set; } = new();
}
