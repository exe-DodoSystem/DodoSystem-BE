using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations
{
    public class EmployeeBonusDeductionEntryConfiguration : IEntityTypeConfiguration<EmployeeBonusDeductionEntry>
    {
        public void Configure(EntityTypeBuilder<EmployeeBonusDeductionEntry> entity)
        {
            entity.ToTable("EmployeeBonusDeductionEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.Employee)
                .WithMany()
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_EmployeeBonusDeductionEntries_Employees");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_EmployeeBonusDeductionEntries_Users");
        }
    }
}
