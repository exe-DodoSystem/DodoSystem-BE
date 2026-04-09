namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class TodayAttendanceDto
{
    public bool HasCheckedIn { get; set; }
    public bool HasCheckedOut { get; set; }

    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public string? CheckInSelfieUrl { get; set; }

    public string? Status { get; set; }      // Present | Late | null (chưa có)
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }

    public string? ApprovalStatus { get; set; } // null | Pending | Approved | Rejected
}
