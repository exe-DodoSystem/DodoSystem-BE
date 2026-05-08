namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Đánh dấu các sự kiện (Event) đã được xử lý phía Consumer để chống xử lý trùng lặp (Idempotent Consumer).
/// </summary>
public partial class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
