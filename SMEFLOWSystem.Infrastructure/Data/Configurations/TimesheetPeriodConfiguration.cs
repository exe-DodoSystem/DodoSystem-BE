using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class TimesheetPeriodConfiguration : IEntityTypeConfiguration<TimesheetPeriod>
{
    public void Configure(EntityTypeBuilder<TimesheetPeriod> builder)
    {
        builder.ToTable("TimesheetPeriods");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.TenantId, e.Year, e.Month })
            .IsUnique()
            .HasDatabaseName("IX_TimesheetPeriods_Tenant_Year_Month");

        builder.Property(e => e.Month).IsRequired();
        builder.Property(e => e.Year).IsRequired();
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.IsLocked).IsRequired().HasDefaultValue(false);
    }
}
