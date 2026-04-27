namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos
{
    public class SubmitPunchRequestDto
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? SelfieUrl { get; set; }
        public string? DeviceId { get; set; }
        public string? PunchType { get; set; } = "Auto";
    }
}
