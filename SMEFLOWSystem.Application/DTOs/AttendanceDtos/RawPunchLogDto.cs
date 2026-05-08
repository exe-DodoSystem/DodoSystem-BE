using System;

namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos
{
    public class RawPunchLogDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public DateTime Timestamp { get; set; }
        public string? DeviceId { get; set; }
        public bool IsProcessed { get; set; }
        public string PunchType { get; set; } = "Auto";
    }
}
