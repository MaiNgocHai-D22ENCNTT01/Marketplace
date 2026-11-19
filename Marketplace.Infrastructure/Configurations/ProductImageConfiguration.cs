using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> entity)
    {
        entity.ToTable("product_images");
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

        entity.Property(x => x.VariantId)
            .HasColumnName("variant_id")
            .HasColumnType("uuid");

        entity.Property(x => x.ImageUrl)
            .HasColumnName("image_url")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.IsPrimary)
            .HasColumnName("is_primary")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        entity.Property(x => x.FeatureVector)
            .HasColumnName("feature_vector")
            .HasColumnType("text");

        entity.HasOne(x => x.Product)
            .WithMany(x => x.ProductImages)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Variant)
            .WithMany(x => x.ProductImages)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
