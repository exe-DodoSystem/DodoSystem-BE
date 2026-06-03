namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class MyMonthSummaryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int WorkDays { get; set; }      // Present + Late + Normal + EarlyLeave
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public decimal TotalOTHours { get; set; }
    public int TotalLateMinutes { get; set; }
}
