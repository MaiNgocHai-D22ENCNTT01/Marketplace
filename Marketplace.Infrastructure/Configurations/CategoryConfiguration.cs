using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.ToTable("categories", table =>
        {
            table.HasCheckConstraint("ck_categories_status", "status IN ('ACTIVE', 'HIDDEN')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.ParentId)
            .HasColumnName("parent_id")
            .HasColumnType("uuid");

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.Slug)
            .HasColumnName("slug")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.HasIndex(x => x.Slug).IsUnique();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(CategoryStatus.ACTIVE)
            .IsRequired();

        entity.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
