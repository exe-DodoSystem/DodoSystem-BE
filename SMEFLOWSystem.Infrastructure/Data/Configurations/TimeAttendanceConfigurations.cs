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
    }
}

public class EmployeeShiftPatternConfiguration : IEntityTypeConfiguration<EmployeeShiftPattern>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftPattern> builder)
    {
        builder.HasKey(x => x.Id);
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
    }
}

public class DailyTimesheetConfiguration : IEntityTypeConfiguration<DailyTimesheet>
{
    public void Configure(EntityTypeBuilder<DailyTimesheet> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StandardWorkingHours).HasColumnType("decimal(18,2)");

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
