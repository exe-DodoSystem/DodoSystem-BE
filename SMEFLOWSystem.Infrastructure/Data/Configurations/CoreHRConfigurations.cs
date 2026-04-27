using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

// public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
// {
//     public void Configure(EntityTypeBuilder<Attendance> entity)
//     {
//         entity.HasKey(e => e.Id).HasName("PK__Attendan__3214EC07446D18B6");
//         entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.WorkDate }, "UQ_Attendance_Per_Day").IsUnique();
//         entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
//         entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
//         entity.Property(e => e.Notes).HasMaxLength(255);
//         entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Present");
//         entity.Property(e => e.CheckInSelfieUrl).HasMaxLength(1000);
//         entity.Property(e => e.CheckOutSelfieUrl).HasMaxLength(1000);
//         entity.Property(e => e.ApprovalStatus).HasMaxLength(30);
//         entity.Property(e => e.ApprovalNotes).HasMaxLength(500);

//         entity.HasOne(d => d.Employee).WithMany(p => p.Attendances).HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Attendances_Employees");
//         entity.HasOne(d => d.Tenant).WithMany(p => p.Attendances).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Attendances_Tenants");
//     }
// }

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07B17A5536");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.CompanyName).HasMaxLength(255); 
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Email).HasMaxLength(100); 
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.Phone).HasMaxLength(50);  
        entity.Property(e => e.Type).IsRequired().HasMaxLength(20).HasDefaultValue("Individual");
        entity.HasOne(d => d.Tenant).WithMany(p => p.Customers).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Customers_Tenants");
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Departme__3214EC070252CA08");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.HasOne(d => d.Tenant).WithMany(p => p.Departments).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Departments_Tenants");
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Employee__3214EC07C73E2C13");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.BaseSalary).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Email).HasMaxLength(100);  
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Phone).HasMaxLength(50);  
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Working");
        entity.HasOne(d => d.Department).WithMany(p => p.Employees).HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Employees_Departments");
        entity.HasOne(d => d.Position).WithMany(p => p.Employees).HasForeignKey(d => d.PositionId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Employees_Positions");
        entity.HasOne(d => d.Tenant).WithMany(p => p.Employees).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Employees_Tenants");
        entity.HasOne(d => d.User).WithMany(p => p.Employees).HasForeignKey(d => d.UserId).HasConstraintName("FK_Employees_Users");
    }
}

public class PayrollConfiguration : IEntityTypeConfiguration<Payroll>
{
    public void Configure(EntityTypeBuilder<Payroll> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Payrolls__3214EC07E77CFA45");
        entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Year, e.Month }, "UQ_Payroll_Month").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.BaseSalarySnapshot).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Bonus).HasDefaultValue(0m).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.BasePay).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Deduction).HasDefaultValue(0m).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Notes).HasMaxLength(255);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Draft");
        entity.Property(e => e.TotalSalary).HasColumnType("decimal(18, 2)");
        entity.HasOne(d => d.Employee).WithMany(p => p.Payrolls).HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Payrolls_Employees");
        entity.HasOne(d => d.Tenant).WithMany(p => p.Payrolls).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Payrolls_Tenants");
    }
}

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Position__3214EC07176C885B");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.HasOne(d => d.Department).WithMany(p => p.Positions).HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Positions_Departments");
        entity.HasOne(d => d.Tenant).WithMany(p => p.Positions).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Positions_Tenants");
    }
}

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Tenants__3214EC0740A20BD5");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Active");
        entity.HasOne(e => e.OwnerUser).WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
