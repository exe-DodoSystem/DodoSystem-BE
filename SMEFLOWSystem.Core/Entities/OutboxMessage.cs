using System;

namespace SMEFLOWSystem.Core.Entities;

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

    public string Status { get; set; } = "Pending"; // Pending, Processing, Processed, Failed

    public int RetryCount { get; set; }

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
