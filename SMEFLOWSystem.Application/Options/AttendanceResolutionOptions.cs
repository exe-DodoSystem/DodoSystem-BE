namespace SMEFLOWSystem.Application.Options;

public class AttendanceResolutionOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 500;
    public int DedupWindowMinutes { get; set; } = 2;
    public int MaxBatchesPerRun { get; set; } = 10;
}
