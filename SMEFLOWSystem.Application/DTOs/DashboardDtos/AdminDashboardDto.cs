using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class AdminDashboardDto
{
    public int TotalEmployees { get; set; }
    public List<DepartmentEmployeeCountDto> EmployeesByDepartment { get; set; } = new();
    public TodayAttendanceSummaryDto TodayAttendance { get; set; } = new();
    public MonthlyAttendanceStatsDto MonthlyStats { get; set; } = new();
    public PayrollSummaryDto PayrollSummary { get; set; } = new();
    public int PendingAppealsCount { get; set; }
    public List<AlertItemDto> Alerts { get; set; } = new();
}
