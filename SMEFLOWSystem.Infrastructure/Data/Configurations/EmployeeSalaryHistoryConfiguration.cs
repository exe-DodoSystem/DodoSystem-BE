using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations
{
    public class EmployeeSalaryHistoryConfiguration : IEntityTypeConfiguration<EmployeeSalaryHistory>
    {
        public void Configure(EntityTypeBuilder<EmployeeSalaryHistory> entity)
        {
            entity.ToTable("EmployeeSalaryHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");

            entity.Property(e => e.OldSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NewSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.Employee)
                .WithMany()
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_EmployeeSalaryHistories_Employees");

            entity.HasOne(d => d.ChangedByUser)
                .WithMany()
                .HasForeignKey(d => d.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_EmployeeSalaryHistories_Users");
        }
    }
}
