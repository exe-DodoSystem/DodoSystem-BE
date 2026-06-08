using System;

namespace SMEFLOWSystem.Application.DTOs.HRDtos;

public class ManualMonthlyTimesheetDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public int ActualWorkingDays { get; set; }
    public int AbsentDays { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEarlyLeaveMinutes { get; set; }
    public decimal TotalOTHours { get; set; }
}
