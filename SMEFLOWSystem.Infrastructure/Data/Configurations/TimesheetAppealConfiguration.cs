using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class TimesheetAppealConfiguration : IEntityTypeConfiguration<TimesheetAppeal>
{
    public void Configure(EntityTypeBuilder<TimesheetAppeal> builder)
    {
        builder.ToTable("TimesheetAppeals");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AppealType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RejectReason)
            .HasMaxLength(500);

        builder.Property(e => e.AttachmentUrl)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
