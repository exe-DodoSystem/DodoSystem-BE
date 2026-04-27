using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__RefreshToken__3214EC07");
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
        entity.Property(e => e.RevokeReason).HasMaxLength(255);
        entity.HasIndex(e => new { e.TenantId, e.UserId }, "IX_RefreshTokens_Tenant_User");
        entity.HasIndex(e => new { e.TenantId, e.TokenHash }, "UQ_RefreshTokens_Tenant_TokenHash").IsUnique();
        entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_RefreshTokens_Users");
        entity.HasOne(d => d.Tenant).WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_RefreshTokens_Tenants");
        entity.HasOne(d => d.ReplacedByToken).WithMany().HasForeignKey(d => d.ReplacedByTokenId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RefreshTokens_ReplacedBy");
    }
}

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Invites__3214EC07ABCDEF12");
        entity.HasIndex(e => e.Token, "UQ_Invite_Token").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
        entity.Property(e => e.ExpiryDate).IsRequired();
        entity.Property(e => e.IsUsed).HasDefaultValue(false);
        entity.Property(e => e.Message).HasMaxLength(500);
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.HasOne(d => d.Tenant).WithMany(p => p.Invites).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Invites_Tenants");
        entity.HasOne(d => d.Role).WithMany(p => p.Invites).HasForeignKey(d => d.RoleId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Invites_Roles");
        entity.HasOne(d => d.Department).WithMany(p => p.Invites).HasForeignKey(d => d.DepartmentId).HasConstraintName("FK_Invites_Departments");
        entity.HasOne(d => d.Position).WithMany(p => p.Invites).HasForeignKey(d => d.PositionId).HasConstraintName("FK_Invites_Positions");
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC07B3A63021");
        entity.HasIndex(e => e.Name, "UQ__Roles__737584F671819C23").IsUnique();
        entity.Property(e => e.Description).HasMaxLength(255);
        entity.Property(e => e.IsSystemRole).HasDefaultValue(false);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07D3CEC114");
        entity.HasIndex(e => new { e.TenantId, e.Email }, "UQ_Users_Email_Tenant").IsUnique();
        entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.IsVerified).HasDefaultValue(false);
        entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.AvatarUrl).HasMaxLength(1000);
        entity.HasOne(d => d.Tenant).WithMany(p => p.Users).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Users_Tenants");
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> entity)
    {
        entity.HasKey(e => new { e.UserId, e.RoleId });
        entity.HasOne(d => d.Role).WithMany(p => p.UserRoles).HasForeignKey(d => d.RoleId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UserRoles_Roles");
        entity.HasOne(d => d.User).WithMany(p => p.UserRoles).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UserRoles_Users");
    }
}
