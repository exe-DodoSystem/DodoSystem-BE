namespace SMEFLOWSystem.Application.DTOs.AttendanceDtos;

public class CheckOutRequestDto
{
    /// <summary>Base64 selfie image (optional - used for face verification)</summary>
    public string? SelfieBase64 { get; set; }

    /// <summary>Current GPS latitude of the employee</summary>
    public double Latitude { get; set; }

    /// <summary>Current GPS longitude of the employee</summary>
    public double Longitude { get; set; }
}
