using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class ManagerDashboardDto
{
    public int DeptEmployeeCount { get; set; }
    public List<DepartmentEmployeeCountDto> EmployeesByDepartment { get; set; } = new();
    public TodayAttendanceSummaryDto? DeptTodayAttendance { get; set; }
    public MonthlyAttendanceStatsDto? DeptMonthlyStats { get; set; }
    public int? DraftPayrollCount { get; set; }
    public int? DeptPendingAppealsCount { get; set; }
    public List<AlertItemDto> Alerts { get; set; } = new();
    public List<string> AvailableModules { get; set; } = new();
}
