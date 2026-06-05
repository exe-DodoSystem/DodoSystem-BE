using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class AdminDashboardDto
{
    public int TotalEmployees { get; set; }
    public List<DepartmentEmployeeCountDto> EmployeesByDepartment { get; set; } = new();
    public TodayAttendanceSummaryDto? TodayAttendance { get; set; }
    public MonthlyAttendanceStatsDto? MonthlyStats { get; set; }
    public PayrollSummaryDto? PayrollSummary { get; set; }
    public int? PendingAppealsCount { get; set; }
    public List<AlertItemDto> Alerts { get; set; } = new();
    public List<string> AvailableModules { get; set; } = new();
}
