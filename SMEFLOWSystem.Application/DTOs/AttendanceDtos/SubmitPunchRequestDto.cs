namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos
{
    public class SubmitPunchRequestDto
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        /// <summary>Base64 selfie image (optional - will be uploaded to Cloudinary)</summary>
        public string? SelfieBase64 { get; set; }
        public string? SelfieUrl { get; set; }
        public string? DeviceId { get; set; }
        public string? PunchType { get; set; } = "Auto";
        public bool IsMockLocation { get; set; } = false; // Phát hiện fake GPS từ mobile app
    }
}
