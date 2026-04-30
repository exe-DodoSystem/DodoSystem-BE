using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class HRMonthlyReportItemDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }

    public int TotalWorkDays { get; set; } // So ngay lam viec
    public decimal TotalActualHours { get; set; } // Tong gio lam thuc te
    public decimal TotalOTHours { get; set; } // Tong gio OT
    public int TotalLateMinutes { get; set; } // Tong so phut di tre
    public int TotalEarlyLeaveMinutes { get; set; } // Tong so phut ve som
    public int MissingPunches { get; set; } // So lan quen cham cong (MissingOut/NoShift)
}