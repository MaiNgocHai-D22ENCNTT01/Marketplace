using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.ToTable("permissions");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.Code)
            .HasColumnName("code")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        entity.HasIndex(x => x.Code).IsUnique();

        entity.Property(x => x.Module)
            .HasColumnName("module")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");
    }
}
