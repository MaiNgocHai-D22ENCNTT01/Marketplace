using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_base_price", "base_price >= 0");
            table.HasCheckConstraint("ck_products_status", "status IN ('ACTIVE', 'HIDDEN', 'DISCONTINUED', 'BANNED')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.HasIndex(x => new { x.ShopId, x.CreatedAt, x.Id });
        entity.Property(x => x.ReviewCount).HasColumnName("review_count").HasDefaultValue(0);
        entity.Property(x => x.RatingAvg).HasColumnName("rating_avg").HasPrecision(3,2).HasDefaultValue(0m);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.ShopId)
            .HasColumnName("shop_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.CategoryId)
            .HasColumnName("category_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        entity.Property(x => x.BasePrice)
            .HasColumnName("base_price")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ProductStatus.ACTIVE)
            .IsRequired();

        entity.HasOne(x => x.Shop)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
