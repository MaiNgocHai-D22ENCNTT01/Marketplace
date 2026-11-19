using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("roles", table =>
        {
            table.HasCheckConstraint("ck_roles_name", "name IN ('CUSTOMER', 'SELLER', 'ADMIN', 'SUPER_ADMIN')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        entity.HasIndex(x => x.Name).IsUnique();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");
    }
}
