using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_OutboxMessages");
        entity.ToTable("OutboxMessages");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.EventId).IsRequired();
        entity.Property(e => e.EventType).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Exchange).IsRequired().HasMaxLength(150);
        entity.Property(e => e.RoutingKey).IsRequired().HasMaxLength(150);
        entity.Property(e => e.Payload).IsRequired();
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
        entity.Property(e => e.RetryCount).HasDefaultValue(0);
        entity.Property(e => e.OccurredOnUtc).HasDefaultValueSql("(getutcdate())");
        entity.Property(e => e.LastError).HasMaxLength(4000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
        
        entity.HasIndex(e => e.EventId).HasDatabaseName("UX_OutboxMessages_EventId").IsUnique();
        entity.HasIndex(e => new { e.Status, e.NextAttemptOnUtc }).HasDatabaseName("IX_OutboxMessages_Status_NextAttemptOnUtc");
        entity.HasIndex(e => e.OccurredOnUtc).HasDatabaseName("IX_OutboxMessages_OccurredOnUtc");
        entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("IX_OutboxMessages_TenantId_Status");
        entity.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_OutboxMessages_Tenants");
    }
}

public class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_ProcessedEvents");
        entity.ToTable("ProcessedEvents");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.EventId).IsRequired();
        entity.Property(e => e.ConsumerName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ProcessedAtUtc).HasDefaultValueSql("(getutcdate())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
        entity.HasIndex(e => new { e.EventId, e.ConsumerName }).HasDatabaseName("UX_ProcessedEvents_EventId_Consumer").IsUnique();
        entity.HasIndex(e => e.ProcessedAtUtc).HasDatabaseName("IX_ProcessedEvents_ProcessedAtUtc");
    }
}

public class TenantAttendanceSettingConfiguration : IEntityTypeConfiguration<TenantAttendanceSetting>
{
    public void Configure(EntityTypeBuilder<TenantAttendanceSetting> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__TenantAttendanceSetting__3214EC07");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.CheckInRadiusMeters).HasDefaultValue(100);
        entity.Property(e => e.LateThresholdMinutes).HasDefaultValue(10);
        entity.Property(e => e.EarlyLeaveThresholdMinutes).HasDefaultValue(10);
        entity.Property(e => e.Latitude).HasColumnType("float");
        entity.Property(e => e.Longitude).HasColumnType("float");
        entity.HasOne(d => d.Tenant).WithOne(t => t.AttendanceSetting).HasForeignKey<TenantAttendanceSetting>(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_TenantAttendanceSettings_Tenants");
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Notifications__3214EC07");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(50).HasDefaultValue("General");
        entity.Property(e => e.IsRead).HasDefaultValue(false);
        entity.HasIndex(e => new { e.RecipientUserId, e.IsRead }).HasDatabaseName("IX_Notifications_RecipientUser_IsRead");
        entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_Notifications_CreatedAt");
        entity.HasOne(d => d.RecipientUser).WithMany().HasForeignKey(d => d.RecipientUserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Notifications_Users");
        entity.HasOne(d => d.Tenant).WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Notifications_Tenants");
    }
}
