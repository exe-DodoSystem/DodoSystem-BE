namespace SMEFLOWSystem.Application.Options;

public class AttendanceResolutionOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 500;
    public int DedupWindowMinutes { get; set; } = 2;
    // Max lệch phút cho phép khi map log vào ca (proximity window)
    public int ProximityWindowMinutes { get; set; } = 240;
    public int MaxBatchesPerRun { get; set; } = 10;
}
