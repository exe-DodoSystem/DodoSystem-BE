using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class RawPunchLogConfiguration : IEntityTypeConfiguration<RawPunchLog>
{
    public void Configure(EntityTypeBuilder<RawPunchLog> builder)
    {
        builder.ToTable("RawPunchLogs");
        
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PunchType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Timestamp)
            .IsRequired();
            
        // Index TỐI QUAN TRỌNG: 
        // Khi Job chạy, nó sẽ select theo EmployeeId và khoảng thời gian (Timestamp)
        // Nếu không có Index này, quét 1 triệu dòng log mỗi đêm thì Server Database sẽ đứt bóng.
        builder.HasIndex(e => new { e.EmployeeId, e.Timestamp })
            .HasDatabaseName("IX_RawPunchLogs_Employee_Timestamp");
            
        // Quan hệ với Employee (bảng 1 nhiều, không cho xóa Cascade để bảo toàn log lịch sử)
        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
