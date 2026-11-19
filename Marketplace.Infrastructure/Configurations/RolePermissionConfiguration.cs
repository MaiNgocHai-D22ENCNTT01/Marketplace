using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> entity)
    {
        entity.ToTable("role_permissions");
        entity.HasKey(x => new { x.RoleId, x.PermissionId });

        entity.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.PermissionId)
            .HasColumnName("permission_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.HasOne(x => x.Role)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Permission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
