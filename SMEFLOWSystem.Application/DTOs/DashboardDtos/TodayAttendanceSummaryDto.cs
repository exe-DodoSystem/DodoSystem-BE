using System;

namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class TodayAttendanceSummaryDto
{
    public DateOnly WorkDate { get; set; }
    public int CheckedIn { get; set; }    // Present + Late + EarlyLeave + Normal
    public int Absent { get; set; }       // Status = Absent
    public int Late { get; set; }         // Status = Late
    public int MissingOut { get; set; }   // Status = MissingOut
    public int OnLeave { get; set; }      // Status = OnLeave
    public int TotalExpected { get; set; } // Tổng NV có timesheet hôm nay
}
