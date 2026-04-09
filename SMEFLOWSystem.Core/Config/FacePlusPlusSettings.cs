namespace SMEFLOWSystem.Core.Config;

public class FacePlusPlusSettings
{
    public string BaseUrl { get; set; } = "https://api-us.faceplusplus.com";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public double ConfidenceThreshold { get; set; } = 80.0;
}
