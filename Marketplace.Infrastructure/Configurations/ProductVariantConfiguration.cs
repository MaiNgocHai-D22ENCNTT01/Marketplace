using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> entity)
    {
        entity.ToTable("product_variants", table =>
        {
            table.HasCheckConstraint("ck_product_variants_price", "price >= 0");
            table.HasCheckConstraint("ck_product_variants_stock_quantity", "stock_quantity >= 0");
            table.HasCheckConstraint("ck_product_variants_status", "status IN ('ACTIVE', 'INACTIVE')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Sku)
            .HasColumnName("sku")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.HasIndex(x => x.Sku).IsUnique();

        entity.Property(x => x.Attributes)
            .HasColumnName("attributes")
            .HasColumnType("json")
            .IsRequired();

        entity.Property(x => x.Price)
            .HasColumnName("price")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.StockQuantity)
            .HasColumnName("stock_quantity")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ProductVariantStatus.ACTIVE)
            .IsRequired();

        entity.HasOne(x => x.Product)
            .WithMany(x => x.ProductVariants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
