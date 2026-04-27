using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class BillingOrderConfiguration : IEntityTypeConfiguration<BillingOrder>
{
    public void Configure(EntityTypeBuilder<BillingOrder> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__BillingOrders__3214EC07");
        entity.HasIndex(e => new { e.TenantId, e.BillingOrderNumber }, "UQ_BillingOrderNumber_Tenant").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.DiscountAmount).HasDefaultValue(0m).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.FinalAmount).HasComputedColumnSql("([TotalAmount]-[DiscountAmount])", true).HasColumnType("decimal(19, 2)");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.BillingDate).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.BillingOrderNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.PaymentStatus).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
    }
}

public class BillingOrderModuleConfiguration : IEntityTypeConfiguration<BillingOrderModule>
{
    public void Configure(EntityTypeBuilder<BillingOrderModule> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__BillingOrderModules__3214EC07");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.LineTotal).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.HasOne(d => d.BillingOrder).WithMany(p => p.BillingOrderModules).HasForeignKey(d => d.BillingOrderId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_BillingOrderModules_BillingOrders");
        entity.HasOne(d => d.Module).WithMany(p => p.BillingOrderModules).HasForeignKey(d => d.ModuleId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_BillingOrderModules_Modules");
    }
}

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Modules__3214EC07");
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.ShortCode).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.MonthlyPrice).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.ShortCode).IsUnique();
    }
}

public class ModuleSubscriptionConfiguration : IEntityTypeConfiguration<ModuleSubscription>
{
    public void Configure(EntityTypeBuilder<ModuleSubscription> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__ModuleSubscriptions__3214EC07");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.HasIndex(e => new { e.TenantId, e.ModuleId }).IsUnique();
        entity.HasOne(d => d.Tenant).WithMany(p => p.ModuleSubscriptions).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_ModuleSubscriptions_Tenants");
        entity.HasOne(d => d.Module).WithMany(p => p.ModuleSubscriptions).HasForeignKey(d => d.ModuleId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_ModuleSubscriptions_Modules");
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Orders__3214EC0734CC9CEB");
        entity.HasIndex(e => new { e.TenantId, e.OrderNumber }, "UQ_OrderNumber_Tenant").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.DiscountAmount).HasDefaultValue(0m).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.FinalAmount).HasComputedColumnSql("([TotalAmount]-[DiscountAmount])", true).HasColumnType("decimal(19, 2)");
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.OrderDate).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.OrderNumber).HasMaxLength(50);
        entity.Property(e => e.PaymentStatus).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("New");
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
        entity.HasOne(d => d.Customer).WithMany(p => p.Orders).HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Orders_Customers");
        entity.HasOne(d => d.Tenant).WithMany(p => p.Orders).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Orders_Tenants");
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__OrderIte__3214EC0756359D82");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Description).IsRequired().HasMaxLength(255);
        entity.Property(e => e.TotalPrice).HasComputedColumnSql("([Quantity]*[UnitPrice])", true).HasColumnType("decimal(29, 2)");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
        entity.HasOne(d => d.Order).WithMany(p => p.OrderItems).HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_OrderItems_Orders");
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.Gateway, e.GatewayTransactionId }).IsUnique();
        entity.HasIndex(e => e.BillingOrderId);
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.Gateway).IsRequired().HasMaxLength(30);
        entity.Property(e => e.GatewayTransactionId).IsRequired().HasMaxLength(64);
        entity.Property(e => e.GatewayResponseCode).HasMaxLength(20);
        entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
        entity.Property(e => e.RawData).HasMaxLength(4000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.HasOne<BillingOrder>().WithMany().HasForeignKey(e => e.BillingOrderId).OnDelete(DeleteBehavior.ClientSetNull);
    }
}
