using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class EmployeeLeaveBalanceConfiguration : IEntityTypeConfiguration<EmployeeLeaveBalance>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveBalance> builder)
    {
        builder.ToTable("EmployeeLeaveBalances");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.EmployeeId)
            .IsRequired();
            
        builder.Property(x => x.LeaveTypeId)
            .IsRequired();
            
        builder.Property(x => x.Year)
            .IsRequired();
            
        builder.Property(x => x.TotalDays)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
            
        builder.Property(x => x.UsedDays)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
            
        builder.Property(x => x.RemainingDays)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year })
            .IsUnique()
            .HasDatabaseName("IX_EmployeeLeaveBalances_Employee_LeaveType_Year");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LeaveType>()
            .WithMany()
            .HasForeignKey(x => x.LeaveTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
