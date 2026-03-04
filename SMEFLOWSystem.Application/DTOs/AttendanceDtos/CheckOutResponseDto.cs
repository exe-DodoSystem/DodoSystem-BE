namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class CheckOutResponseDto
{
    public Guid Id { get; set; }
    public string EmployeeFullName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckOutSelfieUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? EarlyLeaveMinutes { get; set; }
    public string? ApprovalStatus { get; set; }
    public string Message { get; set; } = "Check-out thành công!";
}
