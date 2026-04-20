namespace SMEFLOWSystem.Core.Entities;

public partial class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
