namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class MonthlyAttendanceStatsDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalWorkDays { get; set; }      // Số ngày làm việc (Present/Late/Normal/EarlyLeave)
    public int TotalAbsentDays { get; set; }
    public decimal TotalOTHours { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEmployeeRecords { get; set; } // Tổng số timesheet record (debug/info)
}
