using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class ManualPunchRequestDto
{
    public Guid EmployeeId { get; set; }
    public DateTime Timestamp { get; set; }
    public string PunchType { get; set; } = "Auto"; // "In", "Out", or "Auto"
    public string Reason { get; set; } = string.Empty; // Lý do chỉnh sửa công
}
