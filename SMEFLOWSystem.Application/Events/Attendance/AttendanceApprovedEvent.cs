namespace SMEFLOWSystem.Application.Events.Attendance;

public sealed class AttendanceApprovedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public Guid AttendanceId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid TenantId { get; init; }
    public Guid ApprovedByUserId { get; init; }
    public string ApprovalStatus { get; init; } = "Approved";
    public string? ApprovalNotes { get; init; }
    public string? CorrelationId { get; init; }
}
