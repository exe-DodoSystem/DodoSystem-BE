namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeFullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }

    public DateOnly WorkDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    // GPS
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }

    // Selfie URLs (for face verification review)
    public string? CheckInSelfieUrl { get; set; }
    public string? CheckOutSelfieUrl { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }

    // Approval workflow
    public string? ApprovalStatus { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
