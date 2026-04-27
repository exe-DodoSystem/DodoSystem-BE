using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.LeaveType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        // Map quan hệ cha con
        builder.HasMany(e => e.Segments)
            .WithOne(e => e.LeaveRequest)
            .HasForeignKey(e => e.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade); // Khi xóa đơn nghỉ, tự động xóa chi tiết các segment nghỉ
    }
}

public class LeaveRequestSegmentConfiguration : IEntityTypeConfiguration<LeaveRequestSegment>
{
    public void Configure(EntityTypeBuilder<LeaveRequestSegment> builder)
    {
        builder.ToTable("LeaveRequestSegments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.HoursRequested)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasIndex(e => new { e.LeaveRequestId, e.TargetShiftSegmentId })
            .IsUnique()
            .HasDatabaseName("IX_LeaveRequestSegments_UniqueSegment");
    }
}
