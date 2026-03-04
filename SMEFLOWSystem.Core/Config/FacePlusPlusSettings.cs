namespace SMEFLOWSystem.Core.Config;

public class FacePlusPlusSettings
{
    /// <summary>US endpoint: https://api-us.faceplusplus.com</summary>
    public string BaseUrl { get; set; } = "https://api-us.faceplusplus.com";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    /// <summary>Confidence threshold 0-100. Face++ recommends ~80 for 1e-4 FPR.</summary>
    public double ConfidenceThreshold { get; set; } = 80.0;
}
