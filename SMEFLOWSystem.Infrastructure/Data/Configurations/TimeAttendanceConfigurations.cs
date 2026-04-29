using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasMany(x => x.Segments)
               .WithOne(x => x.Shift)
               .HasForeignKey(x => x.ShiftId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShiftSegmentConfiguration : IEntityTypeConfiguration<ShiftSegment>
{
    public void Configure(EntityTypeBuilder<ShiftSegment> builder)
    {
        builder.HasKey(x => x.Id);
    }
}

public class ShiftPatternConfiguration : IEntityTypeConfiguration<ShiftPattern>
{
    public void Configure(EntityTypeBuilder<ShiftPattern> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.HasMany(x => x.Days)
               .WithOne()
               .HasForeignKey(x => x.ShiftPatternId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShiftPatternDayConfiguration : IEntityTypeConfiguration<ShiftPatternDay>
{
    public void Configure(EntityTypeBuilder<ShiftPatternDay> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.HasOne(x => x.ShiftPattern)
               .WithMany(x => x.Days)
               .HasForeignKey(x => x.ShiftPatternId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ScheduledShift)
               .WithMany()
               .HasForeignKey(x => x.ScheduledShiftId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmployeeShiftPatternConfiguration : IEntityTypeConfiguration<EmployeeShiftPattern>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftPattern> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Employee)
               .WithMany()
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ShiftPattern)
               .WithMany()
               .HasForeignKey(x => x.ShiftPatternId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OvertimeRequestConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedHours).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ApprovedHours).HasColumnType("decimal(18,2)");
        builder.Property(x => x.SystemCalculatedMultiplier).HasColumnType("decimal(18,2)");
        
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne(x => x.Employee)
               .WithMany()
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ApprovedByUser)
               .WithMany()
               .HasForeignKey(x => x.ApprovedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DailyTimesheetConfiguration : IEntityTypeConfiguration<DailyTimesheet>
{
    public void Configure(EntityTypeBuilder<DailyTimesheet> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StandardWorkingHours).HasColumnType("decimal(18,2)");

        builder.Property(x => x.ExpectedShiftSource).HasMaxLength(100);
        builder.Property(x => x.SystemAnomalyFlag).HasMaxLength(50);

        builder.HasOne(x => x.Employee)
               .WithMany()
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ExpectedShift)
               .WithMany()
               .HasForeignKey(x => x.ExpectedShiftId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Segments)
               .WithOne()
               .HasForeignKey(x => x.DailyTimesheetId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AuditLogs)
               .WithOne()
               .HasForeignKey(x => x.DailyTimesheetId)
               .OnDelete(DeleteBehavior.Restrict); 
    }
}

public class DailyTimesheetSegmentConfiguration : IEntityTypeConfiguration<DailyTimesheetSegment>
{
    public void Configure(EntityTypeBuilder<DailyTimesheetSegment> builder)
    {
        builder.HasKey(x => x.Id);
    }
}

public class DailyTimesheetAuditLogConfiguration : IEntityTypeConfiguration<DailyTimesheetAuditLog>
{
    public void Configure(EntityTypeBuilder<DailyTimesheetAuditLog> builder)
    {
        builder.HasKey(x => x.Id);
    }
}
