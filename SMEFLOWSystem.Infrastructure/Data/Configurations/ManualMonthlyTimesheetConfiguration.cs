using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class ManualMonthlyTimesheetConfiguration : IEntityTypeConfiguration<ManualMonthlyTimesheet>
{
    public void Configure(EntityTypeBuilder<ManualMonthlyTimesheet> builder)
    {
        builder.ToTable("ManualMonthlyTimesheets");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TotalOTHours)
            .HasPrecision(18, 2);

        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Month, e.Year })
            .IsUnique();

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
