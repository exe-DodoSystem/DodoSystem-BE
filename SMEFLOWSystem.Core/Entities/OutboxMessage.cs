using System;

namespace SMEFLOWSystem.Core.Entities;

/// <summary>
/// Lưu tạm các sự kiện cần gửi đi để đảm bảo tính nhất quán dữ liệu (Transactional Outbox Pattern).
/// </summary>
public partial class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    /// <summary>Trạng thái gửi message (Pending, Processing, Processed, Failed).</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Số lần thử lại.</summary>
    public int RetryCount { get; set; }

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
