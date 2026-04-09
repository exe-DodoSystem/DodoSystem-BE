using System.ComponentModel.DataAnnotations;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class AttendanceApproveDto
{
    [Required]
    public string ApprovalStatus { get; set; } = string.Empty;

    public string? ApprovalNotes { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }
}
