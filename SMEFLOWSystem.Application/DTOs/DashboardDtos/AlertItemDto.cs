namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class AlertItemDto
{
    public string Type { get; set; } = string.Empty;     // "PendingAppeals", "UnpublishedPayroll", "FrequentAbsent", "MissingOutUnresolved"
    public string Severity { get; set; } = string.Empty; // "High", "Medium", "Low"
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
}
