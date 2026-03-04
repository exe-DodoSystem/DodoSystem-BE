namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class CheckInResponseDto
{
    public Guid Id { get; set; }
    public string EmployeeFullName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? CheckInSelfieUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? LateMinutes { get; set; }
    public string Message { get; set; } = "Check-in thành công!";
}
